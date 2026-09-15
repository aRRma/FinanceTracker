using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Accounts.Catalog;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>Счета: заведение, правка, закрытие, порядок и баланс в списке.</summary>
public sealed class AccountsTests
{
    private static readonly DateOnly OpenedOn = new(2026, 1, 1);

    /// <summary>Заведённый счёт виден в списке со своим начальным остатком.</summary>
    [Fact]
    public async Task Заведённый_счёт_виден_в_списке()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Command("Наличные", 1000m));

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        AccountListItem account = Assert.Single(accounts);
        Assert.Equal("Наличные", account.Name);
        Assert.Equal(Money.Create(1000m, Currency.RUB), account.Balance);
    }

    /// <summary>
    /// Баланс — начальный остаток плюс доходы, минус расходы и исходящие переводы,
    /// плюс входящие. Считается запросом к базе, а не перебором операций.
    /// </summary>
    [Fact]
    public async Task Баланс_складывается_из_начального_остатка_и_операций()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid cash = await SaveAsync(database, Command("Наличные", 1000m));
        Guid card = await SaveAsync(database, Command("Карта", 0m));

        await AddAsync(database, Expense(cash, 250m));
        await AddAsync(database, Income(cash, 100m));
        await AddAsync(database, Transfer(cash, card, 300m));

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal(550m, Balance(accounts, cash).Amount);
        Assert.Equal(300m, Balance(accounts, card).Amount);
    }

    /// <summary>Удалённая операция в баланс не входит: она исчезла с экранов.</summary>
    [Fact]
    public async Task Удалённая_операция_в_баланс_не_входит()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid cash = await SaveAsync(database, Command("Наличные", 1000m));
        Transaction expense = Expense(cash, 250m);
        expense.Delete(DateTimeOffset.UtcNow);

        await AddAsync(database, expense);

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal(1000m, Balance(accounts, cash).Amount);
    }

    /// <summary>Имя счёта занято — сохранение блокируется, регистр и пробелы не спасают.</summary>
    [Fact]
    public async Task Занятое_имя_счёта_блокирует_сохранение()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Command("Наличные", 0m));

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, Command("  наличные  ", 0m)));

        Assert.Equal(Invariant.NameUnique, error.Invariant);
    }

    /// <summary>Переименование в собственное имя проходит: сам с собой счёт не конфликтует.</summary>
    [Fact]
    public async Task Счёт_не_конфликтует_сам_с_собой()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid key = await SaveAsync(database, Command("Наличные", 0m));

        await SaveAsync(database, Command("Наличные", 500m) with { Key = key });

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal(500m, Balance(accounts, key).Amount);
    }

    /// <summary>
    /// Правка только регистра имени сохраняется. Уникальность имён регистра
    /// не различает, и сравнение «изменилось ли имя» тем же способом молча
    /// отбрасывало бы такую правку.
    /// </summary>
    [Fact]
    public async Task Смена_регистра_в_имени_сохраняется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid key = await SaveAsync(database, Command("наличные", 0m));

        await SaveAsync(database, Command("Наличные", 0m) with { Key = key });

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal("Наличные", accounts.Single(account => account.Key == key).Name);
    }

    /// <summary>Отрицательный начальный остаток допустим: долг по карте тоже остаток.</summary>
    [Fact]
    public async Task Отрицательный_начальный_остаток_сохраняется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid key = await SaveAsync(database, Command("Карта кредитная", -12_500.50m));

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal(-12_500.50m, Balance(accounts, key).Amount);
    }

    /// <summary>
    /// Валюта заперта первой же операцией — даже удалённой: она записана
    /// в старой валюте, и смена переписала бы её задним числом.
    /// </summary>
    [Fact]
    public async Task Валюта_запирается_операцией_включая_удалённую()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid key = await SaveAsync(database, Command("Наличные", 0m));
        Transaction expense = Expense(key, 10m);
        expense.Delete(DateTimeOffset.UtcNow);

        await AddAsync(database, expense);

        AccountCard? card = await database.Resolve<IAccountCardQuery>().ReadAsync(key);

        Assert.NotNull(card);
        Assert.True(card.CurrencyLocked);

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, Command("Наличные", 0m) with { Key = key, Currency = Currency.USD }));

        Assert.Equal(Invariant.CurrencyFixedOnceUsed, error.Invariant);
    }

    /// <summary>
    /// Счёт зачисления перевода — тоже сторона операции: его валюта заперта,
    /// а дата перевода ограничивает сдвиг даты открытия. Выборка идёт по двум
    /// индексам порознь, и вторую сторону легко потерять.
    /// </summary>
    [Fact]
    public async Task Счёт_зачисления_считается_стороной_операции()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid source = await SaveAsync(database, Command("Наличные", 1000m));
        Guid target = await SaveAsync(database, Command("Карта", 0m));

        await AddAsync(database, Transfer(source, target, 300m));

        AccountCard? card = await database.Resolve<IAccountCardQuery>().ReadAsync(target);

        Assert.NotNull(card);
        Assert.True(card.CurrencyLocked);
        Assert.Equal(Today, card.EarliestTransactionOn);
    }

    /// <summary>Закрытие обратимо, и ненулевой баланс ему не мешает.</summary>
    [Fact]
    public async Task Счёт_закрывается_и_открывается_обратно()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid key = await SaveAsync(database, Command("Карта", 1000m));

        await SaveAsync(database, Command("Карта", 1000m) with { Key = key, IsClosed = true });
        Assert.True(await IsClosedAsync(database, key));

        await SaveAsync(database, Command("Карта", 1000m) with { Key = key, IsClosed = false });
        Assert.False(await IsClosedAsync(database, key));
    }

    /// <summary>
    /// Закрытие счёта с деньгами требует подтверждения; с нулевым балансом и у уже
    /// закрытого счёта подтверждать нечего.
    /// </summary>
    [Fact]
    public async Task Закрытие_счёта_с_деньгами_предупреждает()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid rich = await SaveAsync(database, Command("Карта", 1_000m));
        Guid empty = await SaveAsync(database, Command("Пустая", 0m));
        Guid closed = await SaveAsync(database, Command("Старая", 500m) with { IsClosed = true });

        AccountViewModel model = Model(database);

        await model.LoadAsync(rich);
        Assert.Null(model.ClosingWarning);
        model.IsClosed = true;
        Assert.Contains(Money.Create(1_000m, Currency.RUB).Display, model.ClosingWarning, StringComparison.Ordinal);

        await model.LoadAsync(empty);
        model.IsClosed = true;
        Assert.Null(model.ClosingWarning);

        await model.LoadAsync(closed);
        Assert.True(model.IsClosed);
        Assert.Null(model.ClosingWarning);
    }

    /// <summary>Порядок задаётся перетаскиванием и сохраняется целым списком.</summary>
    [Fact]
    public async Task Порядок_счетов_сохраняется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid first = await SaveAsync(database, Command("Первый", 0m));
        Guid second = await SaveAsync(database, Command("Второй", 0m));
        Guid third = await SaveAsync(database, Command("Третий", 0m));

        await database.Resolve<IReorderAccountsHandler>().HandleAsync([third, first, second]);

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal([third, first, second], accounts.Select(account => account.Key));
    }

    /// <summary>Карточка несуществующего счёта — пусто, а не исключение.</summary>
    [Fact]
    public async Task Карточка_несуществующего_счёта_пуста()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Assert.Null(await database.Resolve<IAccountCardQuery>().ReadAsync(Guid.CreateVersion7()));
    }

    private static SaveAccountCommand Command(string name, decimal openingBalance) =>
        new()
        {
            Name = name,
            Type = AccountType.Cash,
            Currency = Currency.RUB,
            OpeningBalance = openingBalance,
            OpenedOn = OpenedOn,
            ExcludedFromTotals = false,
            IsClosed = false
        };

    private static AccountViewModel Model(TestDatabase database) => new(
        database.Resolve<IAccountCardQuery>(),
        database.Resolve<ISaveAccountHandler>(),
        database.Resolve<Finance.Application.Infrastructure.IClock>());

    private static Task<Guid> SaveAsync(TestDatabase database, SaveAccountCommand command) =>
        database.Resolve<ISaveAccountHandler>().HandleAsync(command);

    private static Money Balance(IReadOnlyList<AccountListItem> accounts, Guid key) =>
        accounts.Single(account => account.Key == key).Balance;

    private static async Task<bool> IsClosedAsync(TestDatabase database, Guid key)
    {
        AccountCard? card = await database.Resolve<IAccountCardQuery>().ReadAsync(key);

        return card!.IsClosed;
    }

    private static async Task AddAsync(TestDatabase database, Transaction transaction)
    {
        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        context.Transactions.Add(transaction.ToRow());

        await context.SaveChangesAsync();
    }

    private static Transaction Expense(Guid account, decimal amount) =>
        Transaction.Create(
            TransactionKind.Expense, account, Money.Create(amount, Currency.RUB),
            targetAccountKey: null, targetAmount: null, categoryKey: Guid.CreateVersion7(),
            placeKey: null, occurredOn: Today, note: null, Today, DateTimeOffset.UtcNow);

    private static Transaction Income(Guid account, decimal amount) =>
        Transaction.Create(
            TransactionKind.Income, account, Money.Create(amount, Currency.RUB),
            targetAccountKey: null, targetAmount: null, categoryKey: Guid.CreateVersion7(),
            placeKey: null, occurredOn: Today, note: null, Today, DateTimeOffset.UtcNow);

    private static Transaction Transfer(Guid from, Guid to, decimal amount) =>
        Transaction.Create(
            TransactionKind.Transfer, from, Money.Create(amount, Currency.RUB),
            targetAccountKey: to, targetAmount: Money.Create(amount, Currency.RUB), categoryKey: null,
            placeKey: null, occurredOn: Today, note: null, Today, DateTimeOffset.UtcNow);

    // Фиксированная, а не из часов: тест не должен зависеть от того,
    // в какую сторону от полуночи по UTC его запустили
    private static DateOnly Today => new(2026, 8, 25);
}
