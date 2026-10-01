using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Deletion;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Операции: запись, правка, удаление, места из формы и подстановка счёта.
/// </summary>
public sealed class TransactionsTests
{
    /// <summary>
    /// Расход, доход и перевод меняют балансы обоих счетов, как положено виду.
    /// </summary>
    [Fact]
    public async Task Записанные_операции_меняют_балансы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid card = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Expense(cash, 250m));
        await given.SaveAsync(given.Income(cash, 100m));
        await given.SaveAsync(given.Transfer(cash, card, 300m));

        Assert.Equal(550m, (await given.BalanceAsync(cash)).Amount);
        Assert.Equal(300m, (await given.BalanceAsync(card)).Amount);
    }

    /// <summary>
    /// Сумма пишется в валюте счёта списания: команда валюты не несёт.
    /// </summary>
    [Fact]
    public async Task Сумма_берёт_валюту_счёта()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 100m, Currency.EUR);

        await given.SaveAsync(given.Expense(euro, 40m));

        Assert.Equal(Money.Create(60m, Currency.EUR), await given.BalanceAsync(euro));
    }

    /// <summary>
    /// Перевод между валютами несёт две суммы, каждая в валюте своей стороны.
    /// </summary>
    [Fact]
    public async Task Перевод_между_валютами_зачисляет_вторую_сумму()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 500m, Currency.EUR);
        Guid rub = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Transfer(euro, rub, 100m, targetAmount: 9_000m));

        Assert.Equal(Money.Create(400m, Currency.EUR), await given.BalanceAsync(euro));
        Assert.Equal(Money.Create(9_000m, Currency.RUB), await given.BalanceAsync(rub));
    }

    /// <summary>
    /// Перевод между валютами без суммы зачисления — ошибка формы, а не ввода.
    /// </summary>
    [Fact]
    public async Task Перевод_между_валютами_без_суммы_зачисления_отвергается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 500m, Currency.EUR);
        Guid rub = await given.AccountAsync("Карта");

        await Assert.ThrowsAsync<ArgumentException>(() => given.SaveAsync(given.Transfer(euro, rub, 100m)));
    }

    /// <summary>
    /// Категория не того вида — доменное правило, показанное пользователю.
    /// </summary>
    [Fact]
    public async Task Расход_с_доходной_категорией_отвергается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => given.SaveAsync(given.Expense(cash, 10m) with { CategoryKey = given.IncomeCategory }));

        Assert.Equal(Invariant.CategoryKindMatchesTransaction, error.Invariant);
    }

    /// <summary>
    /// Дата раньше открытия счёта отвергается — обработчик подал домену счёт.
    /// </summary>
    [Fact]
    public async Task Дата_раньше_открытия_счёта_отвергается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => given.SaveAsync(given.Expense(cash, 10m, on: TransactionFixture.OpenedOn.AddDays(-1))));

        Assert.Equal(Invariant.TransactionNotBeforeAccountOpened, error.Invariant);
    }

    /// <summary>
    /// Заблокированный счёт в новую операцию не попадает, а уже записанная на нём
    /// операция правится свободно: обработчик подал домену прежнее состояние.
    /// </summary>
    [Fact]
    public async Task Заблокированный_счёт_запрещён_в_новой_операции_и_разрешён_в_старой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid existing = await given.SaveAsync(given.Expense(cash, 100m));

        await given.CloseAsync(cash);

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => given.SaveAsync(given.Expense(cash, 10m)));

        Assert.Equal(Invariant.ClosedAccountNotInNewTransaction, error.Invariant);

        await given.SaveAsync(given.Expense(cash, 150m) with { Key = existing });

        Assert.Equal(850m, (await given.BalanceAsync(cash)).Amount);
    }

    /// <summary>
    /// Правка меняет вид целиком: расход становится переводом, категория исчезает, второй счёт появляется.
    /// </summary>
    [Fact]
    public async Task Правка_меняет_вид_операции()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid card = await given.AccountAsync("Карта");
        Guid key = await given.SaveAsync(given.Expense(cash, 200m, place: "Пятёрочка"));

        await given.SaveAsync(given.Transfer(cash, card, 200m) with { Key = key });

        TransactionCard? card1 = await given.Database.Resolve<ITransactionCardQuery>().ReadAsync(key);

        Assert.NotNull(card1);
        Assert.Equal(TransactionKind.Transfer, card1.Kind);
        Assert.Null(card1.CategoryKey);
        Assert.Null(card1.PlaceName);
        Assert.Equal(card, card1.TargetAccountKey);
        Assert.Equal(200m, (await given.BalanceAsync(card)).Amount);
    }

    /// <summary>
    /// Удаление мягкое: строка остаётся с надгробием, из балансов и карточки исчезает.
    /// </summary>
    [Fact]
    public async Task Удаление_оставляет_надгробие()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid key = await given.SaveAsync(given.Expense(cash, 200m));

        await given.Database.Resolve<IDeleteTransactionsHandler>().HandleAsync([key]);

        Assert.Equal(1000m, (await given.BalanceAsync(cash)).Amount);
        Assert.Null(await given.Database.Resolve<ITransactionCardQuery>().ReadAsync(key));

        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        TransactionRow row = await context.Transactions.IgnoreQueryFilters().SingleAsync(existing => existing.Key == key);

        Assert.NotNull(row.DeletedAtUtc);
    }

    /// <summary>
    /// Повторное удаление — не ошибка: экран мог не успеть обновиться.
    /// </summary>
    [Fact]
    public async Task Повторное_удаление_не_падает()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        Guid key = await given.SaveAsync(given.Expense(cash, 200m));

        IDeleteTransactionsHandler handler = given.Database.Resolve<IDeleteTransactionsHandler>();

        await handler.HandleAsync([key]);
        await handler.HandleAsync([key]);
    }

    /// <summary>
    /// Выделенные в ленте операции удаляются одной правкой: одно оповещение, а не по
    /// одному на строку, — иначе лента перечитывалась бы на каждой и показывала
    /// удаление наполовину. Балансы обоих счетов перевода возвращаются.
    /// </summary>
    [Fact]
    public async Task Несколько_операций_удаляются_одним_изменением()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid card = await given.AccountAsync("Карта");

        Guid[] keys =
        [
            await given.SaveAsync(given.Expense(cash, 200m)),
            await given.SaveAsync(given.Income(cash, 50m)),
            await given.SaveAsync(given.Transfer(cash, card, 300m))
        ];

        List<DataChange> changes = [];
        given.Database.Resolve<IChangeNotifier>().Changed += changes.Add;

        await given.Database.Resolve<IDeleteTransactionsHandler>().HandleAsync(keys);

        Assert.Equal([DataChange.Transactions], changes);
        Assert.Equal(1000m, (await given.BalanceAsync(cash)).Amount);
        Assert.Equal(0m, (await given.BalanceAsync(card)).Amount);
    }

    /// <summary>
    /// Повторное удаление уже удалённых ничего не меняет и экраны не дёргает.
    /// </summary>
    [Fact]
    public async Task Повторное_удаление_нескольких_ничего_не_меняет()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid[] keys = [await given.SaveAsync(given.Expense(cash, 200m)), await given.SaveAsync(given.Expense(cash, 100m))];

        IDeleteTransactionsHandler handler = given.Database.Resolve<IDeleteTransactionsHandler>();
        await handler.HandleAsync(keys);

        List<DataChange> changes = [];
        given.Database.Resolve<IChangeNotifier>().Changed += changes.Add;

        await handler.HandleAsync(keys);

        Assert.Empty(changes);
        Assert.Equal(1000m, (await given.BalanceAsync(cash)).Amount);
    }

    /// <summary>
    /// Подтверждение удаления нескольких операций называет их число и сводит сдвиг
    /// баланса по счёту: две траты с одной карты — одна фраза о карте, а не две.
    /// Уже удалённая в число не входит.
    /// </summary>
    [Fact]
    public async Task Подтверждение_удаления_нескольких_сводит_балансы_по_счетам()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1000m);
        Guid card = await given.AccountAsync("Карта");

        Guid gone = await given.SaveAsync(given.Expense(cash, 50m));
        Guid[] keys =
        [
            gone,
            await given.SaveAsync(given.Expense(cash, 100m)),
            await given.SaveAsync(given.Expense(cash, 200m)),
            await given.SaveAsync(given.Income(cash, 50m)),
            await given.SaveAsync(given.Transfer(cash, card, 300m))
        ];

        await given.Database.Resolve<IDeleteTransactionsHandler>().HandleAsync([gone]);

        TransactionDeletion deletion = await given.Database.Resolve<ITransactionDeletionQuery>().ReadAsync(keys);

        // Сейчас наличных 450, на карте 300: удаление трат и перевода вернёт деньги,
        // удаление дохода их заберёт — выйдет 1000 и 0. Четыре — форма «операции»
        Assert.Equal(4, deletion.Count);
        Assert.Equal(
            string.Format(UiCulture.Current, UiTexts.TransactionDeleteManyConfirmTitle, $"4 {UiTexts.TransactionsCountFew}"),
            deletion.Title);
        Assert.Equal(
            [new BalanceAfterDeletion("Наличные", Money.Create(1000m, Currency.RUB)), new BalanceAfterDeletion("Карта", Money.Create(0m, Currency.RUB))],
            deletion.Balances);
        Assert.EndsWith(UiTexts.TransactionDeleteIrreversible, deletion.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Место заводится из формы один раз: второе упоминание того же названия
    /// в другом регистре ссылается на уже заведённое.
    /// </summary>
    [Fact]
    public async Task Место_заводится_из_формы_и_переиспользуется()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");

        await given.SaveAsync(given.Expense(cash, 10m, place: "Пятёрочка"));
        await given.SaveAsync(given.Expense(cash, 20m, place: "  пятёрочка "));

        PlaceListItem place = Assert.Single(await given.Database.Resolve<IPlacesQuery>().ReadAsync());

        Assert.Equal("Пятёрочка", place.Name);
        Assert.Equal(2, place.TransactionCount);
    }

    /// <summary>
    /// Счёт списания запоминается и подставляется в следующую операцию.
    /// </summary>
    [Fact]
    public async Task Последний_счёт_запоминается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Expense(card, 10m));

        TransactionForm form = await given.Database.Resolve<ITransactionFormQuery>().ReadAsync();

        Assert.Equal(card, form.LastAccountKey);
        Assert.Equal(card.ToString(), await given.Database.Resolve<ILocalSettings>().GetAsync(SettingName.LastAccountKey));
    }

    /// <summary>
    /// Подстановка — для новых операций: правка старой записи последний счёт не трогает.
    /// </summary>
    [Fact]
    public async Task Правка_операции_не_меняет_последний_счёт()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");

        Guid old = await given.SaveAsync(given.Expense(cash, 10m));
        await given.SaveAsync(given.Expense(card, 20m));

        await given.SaveAsync(given.Expense(cash, 15m) with { Key = old });

        TransactionForm form = await given.Database.Resolve<ITransactionFormQuery>().ReadAsync();

        Assert.Equal(card, form.LastAccountKey);
    }

    /// <summary>
    /// Правка перевода меняет валютность: одновалютный становится разновалютным
    /// и получает вторую сумму, разновалютный — одновалютным и теряет её.
    /// </summary>
    [Fact]
    public async Task Правка_перевода_меняет_валютность_в_обе_стороны()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1_000m);
        Guid card = await given.AccountAsync("Карта", 1_000m);
        Guid euro = await given.AccountAsync("Карта евро", 100m, Currency.EUR);

        Guid key = await given.SaveAsync(given.Transfer(cash, card, 200m));

        await given.SaveAsync(given.Transfer(cash, euro, 200m, targetAmount: 2m) with { Key = key });

        Assert.Equal(Money.Create(800m, Currency.RUB), await given.BalanceAsync(cash));
        Assert.Equal(Money.Create(1_000m, Currency.RUB), await given.BalanceAsync(card));
        Assert.Equal(Money.Create(102m, Currency.EUR), await given.BalanceAsync(euro));

        await given.SaveAsync(given.Transfer(cash, card, 300m) with { Key = key });

        Assert.Equal(Money.Create(700m, Currency.RUB), await given.BalanceAsync(cash));
        Assert.Equal(Money.Create(1_300m, Currency.RUB), await given.BalanceAsync(card));
        Assert.Equal(Money.Create(100m, Currency.EUR), await given.BalanceAsync(euro));
    }

    /// <summary>
    /// Неудавшееся сохранение не запоминает счёт: подстановка ссылалась бы на операцию, которой нет.
    /// </summary>
    [Fact]
    public async Task Отвергнутая_операция_не_меняет_последний_счёт()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Expense(cash, 10m));
        await Assert.ThrowsAsync<DomainException>(
            () => given.SaveAsync(given.Expense(card, 10m) with { CategoryKey = given.IncomeCategory }));

        TransactionForm form = await given.Database.Resolve<ITransactionFormQuery>().ReadAsync();

        Assert.Equal(cash, form.LastAccountKey);
    }

    /// <summary>
    /// Форма отдаёт подкатегории с видом группы: перевод без категории, расход — только расходные.
    /// </summary>
    [Fact]
    public async Task Форма_отдаёт_подкатегории_обоих_видов()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        TransactionForm form = await given.Database.Resolve<ITransactionFormQuery>().ReadAsync();

        Assert.Contains(form.Categories, category => category.Kind is CategoryKind.Expense);
        Assert.Contains(form.Categories, category => category.Kind is CategoryKind.Income);
        Assert.All(form.Categories, category => Assert.False(string.IsNullOrEmpty(category.GroupName)));
    }
}
