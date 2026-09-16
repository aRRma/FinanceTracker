using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Balances;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Главный экран: разделы по валютам, подытог и признаки состояния.
/// </summary>
public sealed class BalancesTests
{
    private static readonly DateOnly OpenedOn = new(2026, 1, 1);
    private static readonly DateOnly Today = new(2026, 8, 25);

    /// <summary>
    /// Минус на счёте помечен признаком, а не только знаком в тексте: экран красит
    /// такой баланс смысловым цветом, и различать его по минусу в строке нельзя.
    /// </summary>
    [Fact]
    public async Task Отрицательный_баланс_счёта_помечен()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid card = await SaveAsync(database, Command("Карта", 100m));
        Guid cash = await SaveAsync(database, Command("Наличные", 100m));

        await AddAsync(database, Expense(card, 250m));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        CurrencySection section = Assert.Single(model.Sections);

        Assert.True(Tile(section, card).IsNegative);
        Assert.False(Tile(section, cash).IsNegative);
    }

    /// <summary>
    /// «Доступно к тратам» тоже уходит в минус — когда минус одного счёта больше
    /// остатка на прочих. Подытог считается по тратимым счетам, накопления его
    /// не спасают.
    /// </summary>
    [Fact]
    public async Task Отрицательный_подытог_помечен()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid card = await SaveAsync(database, Command("Карта", 100m));
        await SaveAsync(database, Command("Копилка", 5000m, excludedFromTotals: true));

        await AddAsync(database, Expense(card, 250m));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        CurrencySection section = Assert.Single(model.Sections);

        Assert.True(section.IsAvailableNegative);
    }

    /// <summary>
    /// До первого чтения экран не утверждает ни что счета есть, ни что их нет.
    /// Иначе приглашение завести первый счёт и кнопка «записать операцию» успевали
    /// бы мигнуть на каждом заходе — обе привязаны к этим же признакам.
    /// </summary>
    [Fact]
    public async Task До_чтения_экран_не_говорит_ни_о_счетах_ни_об_их_отсутствии()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        BalancesViewModel model = Model(database);

        Assert.False(model.IsLoaded);
        Assert.False(model.HasAccounts);
        Assert.False(model.IsEmpty);

        await model.LoadAsync();

        Assert.True(model.IsLoaded);
        Assert.True(model.IsEmpty);
        Assert.False(model.HasAccounts);
    }

    /// <summary>
    /// Пока счета в плюсе, ни строка, ни подытог смысловым цветом не красятся.
    /// </summary>
    [Fact]
    public async Task Положительный_баланс_не_помечен()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid cash = await SaveAsync(database, Command("Наличные", 1000m));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        CurrencySection section = Assert.Single(model.Sections);

        Assert.False(section.IsAvailableNegative);
        Assert.False(Tile(section, cash).IsNegative);
    }

    private static BalancesViewModel Model(TestDatabase database) => new(
        database.Resolve<IAccountsQuery>(),
        database.Resolve<IChangeNotifier>());

    private static AccountTile Tile(CurrencySection section, Guid key) =>
        section.Spendable.Concat(section.Savings).Single(tile => tile.Key == key);

    private static SaveAccountCommand Command(string name, decimal openingBalance, bool excludedFromTotals = false) =>
        new()
        {
            Name = name,
            Type = AccountType.Cash,
            Currency = Currency.RUB,
            OpeningBalance = openingBalance,
            OpenedOn = OpenedOn,
            ExcludedFromTotals = excludedFromTotals,
            IsClosed = false
        };

    private static Task<Guid> SaveAsync(TestDatabase database, SaveAccountCommand command) =>
        database.Resolve<ISaveAccountHandler>().HandleAsync(command);

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
}
