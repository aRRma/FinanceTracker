using CsCheck;
using Finance.Application.Features.Report;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Сверка запросов с наивным подсчётом в памяти на случайных историях. Тесты
/// на примерах проверяют придуманные руками случаи, эти — истории до сотни с лишним
/// операций, которых никто не придумывал: валюты, скрытые счета, переводы между ними,
/// возвраты, удалённые операции и границы месяцев вперемешку.
/// </summary>
/// <remarks>
/// На сбое CsCheck печатает зерно и размер истории. Повтор того же случая —
/// <c>dotnet test -e CsCheck_Seed=зерно</c> с фильтром по тесту. Сжать историю
/// до короткой он успевает только за оставшиеся итерации, а число, заданное
/// в коде, переменная <c>CsCheck_Iter</c> не перекрывает: для разбора сбоя
/// <see cref="Iterations"/> поднимается на время правки.
/// </remarks>
public sealed class ModelCheckTests
{
    // Итераций столько, чтобы класс шёл секунды: каждая заводит свою базу
    // со стартовым набором и пишет до сотни с лишним операций. Зерно у каждого
    // прогона новое, и охват копится от прогона к прогону, а не в одном
    private const int Iterations = 30;

    private const int MaxTransactions = 120;

    [Fact]
    public Task Баланс_счёта_сходится_с_подсчётом() =>
        HistoryPlan.Generate(MaxTransactions).SampleAsync(
            static async plan =>
            {
                await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
                History history = await History.WriteAsync(database, plan);
                IAccountsQuery query = database.Resolve<IAccountsQuery>();

                IReadOnlyList<AccountListItem> listed = await query.ReadAsync();

                foreach (AccountRow account in history.Accounts)
                {
                    Money expected = history.Balance(account);

                    // Список и шапка ленты счёта считают баланс разными запросами
                    Assert.Equal(expected, listed.Single(item => item.Key == account.Key).Balance);
                    Assert.Equal(expected, (await query.ReadOneAsync(account.Key))!.Balance);
                }
            },
            iter: Iterations);

    [Fact]
    public Task Страницы_ленты_складываются_в_подсчитанную_ленту() =>
        Gen.Select(HistoryPlan.Generate(MaxTransactions), Gen.Int[1, 25], Gen.Int[-1, 4]).SampleAsync(
            static async (plan, take, viewedIndex) =>
            {
                await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
                History history = await History.WriteAsync(database, plan);
                IFeedQuery query = database.Resolve<IFeedQuery>();

                // Номер вне списка счетов — общая лента
                Guid? viewed = viewedIndex >= 0 && viewedIndex < history.Accounts.Count
                    ? history.Accounts[viewedIndex].Key
                    : null;

                IReadOnlyList<TransactionRow> expected = history.Feed(viewed);
                List<FeedItem> read = [];
                FeedCursor? cursor = null;

                // Страниц не больше, чем строк, плюс пустая: иначе зацикленный курсор
                // повесил бы прогон вместо падения
                for (int pages = 0; pages <= expected.Count + 1; pages++)
                {
                    FeedPage page = await query.ReadAsync(viewed, cursor, take);

                    read.AddRange(page.Items);

                    // Итог дня — за весь день, даже если день не уместился в страницу
                    foreach (DateOnly day in page.Items.Select(static item => item.OccurredOn).Distinct())
                    {
                        Money total = history.DayTotal(day, viewed);
                        Money actual = page.DayTotals.TryGetValue(day, out Money found) ? found : Money.Zero(total.Currency);

                        Assert.Equal(total, actual);
                    }

                    if (!page.HasMore)
                    {
                        break;
                    }

                    cursor = page.Next;
                }

                Assert.Equal(expected.Select(static row => row.Key), read.Select(static item => item.Key));

                for (int i = 0; i < expected.Count; i++)
                {
                    Assert.Equal(history.FeedAmount(expected[i], viewed), read[i].Amount);
                }
            },
            iter: Iterations);

    [Fact]
    public Task Отчёт_сходится_с_подсчётом_на_всех_трёх_уровнях() =>
        Gen.Select(HistoryPlan.Generate(MaxTransactions), Gen.Int[0, 3], Gen.Int[0, 2], Gen.Int[1, 31]).SampleAsync(
            static async (plan, monthIndex, accountsChoice, mask) =>
            {
                await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
                History history = await History.WriteAsync(database, plan);
                IReportQuery query = database.Resolve<IReportQuery>();

                ReportMonth month = ReportMonth.Of(HistoryPlan.First.AddMonths(monthIndex));
                ReportAccounts accounts = Choose(history, accountsChoice, mask);
                IReadOnlyList<TransactionRow> reported = history.Reported(month, accounts);

                IReadOnlyList<ReportTotal> groups = await query.ReadGroupsAsync(month, accounts);

                Assert.Equal(history.HasUncounted(month, accounts), await query.HasUncountedAsync(month, accounts));

                Assert.Equal(
                    Totals(reported, history, static (catalog, row) => catalog[row.CategoryKey!.Value].GroupKey),
                    groups.ToDictionary(static total => total.Key, static total => total.Total.Amount));

                // Порядок — по сведённому итогу, а не по одной из сумм вида
                Assert.Equal(
                    groups.Select(static total => total.Total.Amount).OrderDescending(),
                    groups.Select(static total => total.Total.Amount));

                foreach (ReportTotal group in groups)
                {
                    Assert.Equal(accounts.Currency, group.Total.Currency);

                    IReadOnlyList<ReportTotal> subcategories = await query.ReadSubcategoriesAsync(group.Key, month, accounts);
                    IReadOnlyList<TransactionRow> inGroup =
                        [.. reported.Where(row => history.Catalog[row.CategoryKey!.Value].GroupKey == group.Key)];

                    Assert.Equal(
                        Totals(inGroup, history, static (_, row) => row.CategoryKey!.Value),
                        subcategories.ToDictionary(static total => total.Key, static total => total.Total.Amount));

                    foreach (ReportTotal subcategory in subcategories)
                    {
                        IReadOnlyList<ReportTransaction> lines =
                            await query.ReadTransactionsAsync(subcategory.Key, month, accounts);

                        Assert.Equal(
                            inGroup.Where(row => row.CategoryKey == subcategory.Key).Select(static row => row.Key).Order(),
                            lines.Select(static line => line.Key).Order());
                    }
                }
            },
            iter: Iterations);

    /// <summary>
    /// Набор счетов отчёта: все активные в рублях, все активные в долларах или выбранные
    /// руками по маске. Выбранные бывают и скрытыми, и в чужой валюте — такие выпадают.
    /// </summary>
    private static ReportAccounts Choose(History history, int choice, int mask) =>
        choice switch
        {
            0 => ReportAccounts.Default,
            1 => ReportAccounts.AllActive(Currency.USD),
            _ => ReportAccounts.Chosen(
                history.Accounts[0].Currency,
                [.. history.Accounts.Where((_, i) => (mask & (1 << i)) != 0 || i is 0).Select(static account => account.Key)])
        };

    /// <summary>
    /// Сведённые суммы по ключу строки отчёта.
    /// </summary>
    private static Dictionary<Guid, decimal> Totals(
        IEnumerable<TransactionRow> rows,
        History history,
        Func<CategoryCatalog, TransactionRow, Guid> keyOf)
    {
        Dictionary<Guid, decimal> totals = [];

        foreach (TransactionRow row in rows)
        {
            Guid key = keyOf(history.Catalog, row);
            totals[key] = totals.GetValueOrDefault(key) + history.ReportShare(row);
        }

        return totals;
    }
}
