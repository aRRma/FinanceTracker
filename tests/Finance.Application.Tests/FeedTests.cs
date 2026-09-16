using Finance.Application.Features.Feed;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Лента: порядок, стороны перевода, итоги дня, страницы.
/// </summary>
public sealed class FeedTests
{
    /// <summary>
    /// Порядок — от новых к старым по дате, внутри дня — по моменту записи.
    /// </summary>
    [Fact]
    public async Task Лента_идёт_от_новых_к_старым()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        DateOnly today = given.Today;

        Guid old = await given.SaveAsync(given.Expense(cash, 1m, on: today.AddDays(-2)));
        Guid first = await given.SaveAsync(given.Expense(cash, 2m, on: today));
        Guid second = await given.SaveAsync(given.Expense(cash, 3m, on: today));

        FeedPage page = await given.FeedAsync();

        Assert.Equal([second, first, old], page.Items.Select(item => item.Key));
    }

    /// <summary>
    /// Перевод виден в лентах обоих счетов: у списания минусом в его валюте,
    /// у зачисления плюсом в его валюте. В общей ленте — один раз, со стороны списания.
    /// </summary>
    [Fact]
    public async Task Перевод_показан_с_каждой_стороны_своей_суммой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 500m, Currency.EUR);
        Guid rub = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Transfer(euro, rub, 100m, targetAmount: 9_000m));

        FeedItem fromEuro = Assert.Single((await given.FeedAsync(euro)).Items);
        FeedItem fromRub = Assert.Single((await given.FeedAsync(rub)).Items);
        FeedItem overall = Assert.Single((await given.FeedAsync()).Items);

        Assert.Equal(Money.Restore(-100m, Currency.EUR), fromEuro.Amount);
        Assert.Equal("Карта", fromEuro.Title);

        Assert.Equal(Money.Restore(9_000m, Currency.RUB), fromRub.Amount);
        Assert.Equal("Карта евро", fromRub.Title);

        Assert.Equal(Money.Restore(-100m, Currency.EUR), overall.Amount);
        Assert.Equal("Карта евро", overall.AccountName);
    }

    /// <summary>
    /// Итог дня общей ленты — в рублях, без скрытых из расчётов счетов и чужих валют.
    /// Перевод входит как расход, только если деньги ушли из учитываемых счетов:
    /// на скрытый счёт или в валюту. Перевод между двумя учитываемыми счетами
    /// итог не меняет. Строки при этом видны все.
    /// </summary>
    [Fact]
    public async Task Итог_дня_общей_ленты_считается_по_правилам()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid cash = await given.AccountAsync("Наличные");
        Guid savings = await given.AccountAsync("Копилка", excluded: true);
        Guid euro = await given.AccountAsync("Карта евро", 100m, Currency.EUR);

        await given.SaveAsync(given.Expense(card, 1_250m));
        await given.SaveAsync(given.Income(card, 90_000m));
        await given.SaveAsync(given.Transfer(card, savings, 5_000m));
        await given.SaveAsync(given.Transfer(card, cash, 3_000m));
        await given.SaveAsync(given.Transfer(card, euro, 2_000m, targetAmount: 20m));
        await given.SaveAsync(given.Expense(savings, 700m));
        await given.SaveAsync(given.Expense(euro, 48m));

        FeedPage page = await given.FeedAsync();

        Assert.Equal(7, page.Items.Count);
        Assert.Equal(Money.Restore(81_750m, Currency.RUB), page.DayTotals[given.Today]);
    }

    /// <summary>
    /// Итог дня ленты счёта — в его валюте, обе стороны переводов.
    /// </summary>
    [Fact]
    public async Task Итог_дня_ленты_счёта_включает_зачисления()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 1_000m);
        Guid cash = await given.AccountAsync("Наличные", 1_000m);

        await given.SaveAsync(given.Expense(card, 100m));
        await given.SaveAsync(given.Transfer(cash, card, 300m));

        FeedPage page = await given.FeedAsync(card);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(Money.Restore(200m, Currency.RUB), page.DayTotals[given.Today]);
    }

    /// <summary>
    /// Удалённая операция из ленты исчезает.
    /// </summary>
    [Fact]
    public async Task Удалённая_операция_в_ленту_не_попадает()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        Guid key = await given.SaveAsync(given.Expense(cash, 10m));

        await given.Database.Resolve<Features.Transactions.Card.IDeleteTransactionHandler>().HandleAsync(key);

        Assert.Empty((await given.FeedAsync()).Items);
    }

    /// <summary>
    /// Страницы: лишняя строка говорит, что есть ещё, и в страницу не попадает.
    /// </summary>
    [Fact]
    public async Task Лента_читается_страницами()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");

        for (int index = 0; index < 5; index++)
        {
            await given.SaveAsync(given.Expense(cash, index + 1m));
        }

        FeedPage first = await given.FeedAsync(take: 2);
        FeedPage second = await given.FeedAsync(skip: 2, take: 2);
        FeedPage last = await given.FeedAsync(skip: 4, take: 2);

        Assert.Equal(2, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(2, second.Items.Count);
        Assert.True(second.HasMore);
        Assert.Single(last.Items);
        Assert.False(last.HasMore);
    }

    /// <summary>
    /// День, разрезанный границей страницы: итог в шапке — за весь день на обеих
    /// страницах, а не сумма строк, попавших в страницу.
    /// </summary>
    [Fact]
    public async Task Итог_дня_на_границе_страницы_считается_за_весь_день()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");

        for (int index = 0; index < 5; index++)
        {
            await given.SaveAsync(given.Expense(cash, 10m));
        }

        FeedPage first = await given.FeedAsync(take: 3);
        FeedPage second = await given.FeedAsync(skip: 3, take: 3);

        Money expected = Money.Restore(-50m, Currency.RUB);

        Assert.Equal(expected, first.DayTotals[given.Today]);
        Assert.Equal(expected, second.DayTotals[given.Today]);
    }

    /// <summary>
    /// Место, удалённое из справочника, в строке не показывается — операция как без места.
    /// </summary>
    [Fact]
    public async Task Строка_несёт_название_места_и_группы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");

        await given.SaveAsync(given.Expense(cash, 10m, place: "Пятёрочка"));

        FeedItem item = Assert.Single((await given.FeedAsync()).Items);

        Assert.Equal("Пятёрочка", item.Place);
        Assert.False(string.IsNullOrEmpty(item.Group));
        Assert.False(string.IsNullOrEmpty(item.Title));
        Assert.False(string.IsNullOrEmpty(item.Icon));
    }

    /// <summary>
    /// Модель ленты режет строки на дни и дочитывает страницы в уже показанный день,
    /// не заводя вторую шапку с той же датой.
    /// </summary>
    [Fact]
    public async Task Модель_ленты_раскладывает_страницы_по_дням()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1_000m);
        DateOnly today = given.Today;

        await given.SaveAsync(given.Expense(cash, 1m, on: today.AddDays(-1)));
        await given.SaveAsync(given.Expense(cash, 2m, on: today));
        await given.SaveAsync(given.Expense(cash, 3m, on: today));

        FeedViewModel model = new(
            new PagedFeed(given.Database.Resolve<IFeedQuery>(), pageSize: 2),
            given.Database.Resolve<IAccountsQuery>(),
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(cash);

        Assert.True(model.HasMore);
        Assert.Single(model.Days);
        Assert.Equal(2, model.Days[0].Count);
        Assert.Equal("Наличные", model.AccountName);

        await model.LoadMoreAsync();

        Assert.False(model.HasMore);
        Assert.Equal(2, model.Days.Count);
        Assert.Equal(2, model.Days[0].Count);
        Assert.Single(model.Days[1]);
        Assert.Equal(Money.Restore(-5m, Currency.RUB).DisplaySigned, model.Days[0].Total);
    }

    /// <summary>
    /// Шапка ленты счёта читает один счёт своим запросом, а не весь список.
    /// Баланс обязан сойтись с балансом из списка с обеих сторон перевода:
    /// расход и списание уменьшают, доход и зачисление прибавляют.
    /// </summary>
    [Fact]
    public async Task Баланс_одного_счёта_сходится_со_списком()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 1_000m);
        Guid card = await given.AccountAsync("Карта", 500m);

        await given.SaveAsync(given.Expense(cash, 100m));
        await given.SaveAsync(given.Income(cash, 30m));
        await given.SaveAsync(given.Transfer(cash, card, 200m));
        await given.SaveAsync(given.Transfer(card, cash, 50m));

        IAccountsQuery accounts = given.Database.Resolve<IAccountsQuery>();

        AccountListItem? one = await accounts.ReadOneAsync(cash);

        Assert.NotNull(one);
        Assert.Equal(Money.Create(780m, Currency.RUB), one.Balance);
        Assert.Equal(await given.BalanceAsync(cash), one.Balance);
        Assert.Equal("Наличные", one.Name);
        Assert.Null(await accounts.ReadOneAsync(Guid.CreateVersion7()));

        FeedViewModel model = new(
            given.Database.Resolve<IFeedQuery>(),
            accounts,
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(cash);

        Assert.Equal(one.Balance.Display, model.AccountBalance);
    }

    /// <summary>
    /// Цвет суммы в строке: доход зелёным, расход красным, списание перевода —
    /// обычным текстом. Перевод тоже уходит минусом, но тратой не является,
    /// и красный на нём читался бы как ещё один расход.
    /// </summary>
    [Fact]
    public async Task Расход_красится_смысловым_цветом_а_перевод_нет()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные", 100_000m);
        Guid card = await given.AccountAsync("Карта");
        DateOnly today = given.Today;

        await given.SaveAsync(given.Expense(cash, 100m, on: today));
        await given.SaveAsync(given.Income(cash, 200m, on: today));
        await given.SaveAsync(given.Transfer(cash, card, 300m));

        FeedViewModel model = new(
            new PagedFeed(given.Database.Resolve<IFeedQuery>(), pageSize: 20),
            given.Database.Resolve<IAccountsQuery>(),
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(accountKey: null);

        FeedRowItem transfer = model.Days[0].Single(row => row.Icon is "swap");
        FeedRowItem income = model.Days[0].Single(row => row.IsPositive);
        FeedRowItem expense = model.Days[0].Single(row => row.IsExpense);

        Assert.False(transfer.IsExpense);
        Assert.False(transfer.IsPositive);
        Assert.False(income.IsExpense);
        Assert.False(expense.IsPositive);
    }

    /// <summary>
    /// Пустая лента счёта: модель говорит об этом и несёт начальный остаток для строки-заглушки.
    /// </summary>
    [Fact]
    public async Task Пустая_лента_счёта_несёт_начальный_остаток()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 1_240m, Currency.EUR);

        FeedViewModel model = new(
            given.Database.Resolve<IFeedQuery>(),
            given.Database.Resolve<IAccountsQuery>(),
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(euro);

        Assert.True(model.IsEmpty);
        Assert.Equal(Money.Restore(1_240m, Currency.EUR).Display, model.OpeningBalance);
        Assert.Equal("1 января 2026", model.OpenedOn);
        Assert.False(model.IsAccountBalanceNegative);
    }

    /// <summary>
    /// Минус в шапке ленты счёта помечен признаком: экран красит такой баланс
    /// смысловым цветом, и различать его по минусу в строке нельзя.
    /// </summary>
    [Fact]
    public async Task Отрицательный_баланс_в_шапке_ленты_счёта_помечен()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 100m);
        await given.SaveAsync(given.Expense(card, 250m));

        FeedViewModel model = new(
            given.Database.Resolve<IFeedQuery>(),
            given.Database.Resolve<IAccountsQuery>(),
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(card);

        Assert.True(model.IsAccountBalanceNegative);
    }

    /// <summary>
    /// Лента счёта идёт объединением двух выборок, а не условием по двум колонкам:
    /// дизъюнкцию не берёт ни один из двух индексов. Сверяется сам SQL — иначе
    /// правка запроса на <c>OR</c> прошла бы тесты и всплыла бы на большой ленте.
    /// </summary>
    [Fact]
    public async Task Лента_счёта_собирается_объединением_двух_выборок()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string sql = FeedQuery.Compose(context, Guid.CreateVersion7()).ToQueryString();

        Assert.Contains("UNION ALL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"source_account_key\" = @", sql.Split("UNION ALL")[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Общая лента идёт по индексу, а не полным проходом с сортировкой в памяти базы.
    /// У индексов ленты счёта ведущая колонка — сам счёт, и сортировку по всем счетам
    /// они не обслуживают: без отдельного индекса главный список операций перебирает
    /// всю таблицу на каждую страницу. Сверяется план запроса — по составу колонок
    /// этого не видно.
    /// </summary>
    [Fact]
    public async Task Общая_лента_сортируется_по_индексу()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string plan = await QueryPlan.ExplainAsync(given.Database, FeedQuery.Compose(context, accountKey: null).Take(50));

        Assert.Contains("ix_transactions_feed", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("TEMP B-TREE", plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// Урезает страницу до заданного размера: модель просит полсотни, а тесту нужно две.
    /// </summary>
    private sealed class PagedFeed(IFeedQuery inner, int pageSize) : IFeedQuery
    {
        public Task<FeedPage> ReadAsync(Guid? accountKey, int skip, int take, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(accountKey, skip, Math.Min(take, pageSize), cancellationToken);
    }
}
