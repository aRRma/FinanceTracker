using Finance.Application.Features.Report;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// История, записанная в базу по <see cref="HistoryPlan"/>, и наивный подсчёт по ней
/// в памяти. Подсчёт нарочно прямолинеен — перебор всех строк на каждый вопрос:
/// он отвечает на те же вопросы, что запросы, но без их оптимизаций.
/// </summary>
/// <remarks>
/// Операции пишутся доменной фабрикой и одной пачкой, мимо обработчика: обработчик
/// на каждую операцию открывает свою транзакцию и читает счета, а итераций — десятки.
/// Межтабличные правила обработчика план соблюдает сам — даты не раньше открытия
/// счетов, категория подходит виду, заблокированным счёт становится после записи.
/// </remarks>
internal sealed class History
{
    private static readonly DateOnly OpenedOn = new(2026, 1, 1);

    private History(IReadOnlyList<AccountRow> accounts, IReadOnlyList<TransactionRow> rows, CategoryCatalog catalog)
    {
        Accounts = accounts;
        Live = [.. rows.Where(static row => row.DeletedAtUtc is null)];
        Catalog = catalog;
    }

    public IReadOnlyList<AccountRow> Accounts { get; }

    /// <summary>
    /// Неудалённые операции: удалённые не видны ни одному запросу.
    /// </summary>
    public IReadOnlyList<TransactionRow> Live { get; }

    public CategoryCatalog Catalog { get; }

    public static async Task<History> WriteAsync(TestDatabase database, HistoryPlan plan)
    {
        CategoryCatalog catalog = await CategoryCatalog.LoadAsync(database);
        DateTimeOffset createdBase = TestTime.Start.AddDays(-1);

        List<AccountRow> accounts = [];

        for (int i = 0; i < plan.Accounts.Count; i++)
        {
            HistoryPlan.AccountPlan account = plan.Accounts[i];

            accounts.Add(Account.Create(
                $"Счёт {i}", AccountType.Card, AccountColor.Blue, icon: null, account.Currency,
                account.OpeningBalance, OpenedOn, account.Excluded, sortOrder: i,
                HistoryPlan.Today, createdBase).ToRow());
        }

        List<TransactionRow> rows = [];

        foreach (HistoryPlan.TransactionPlan planned in plan.Transactions)
        {
            AccountRow source = accounts[planned.Source];
            bool transfer = planned.Kind is TransactionKind.Transfer;
            AccountRow? target = transfer ? accounts[planned.Target] : null;
            Money amount = Money.Create(planned.Amount, source.Currency);

            Money? targetAmount = target is null ? null
                : target.Currency == source.Currency ? Money.Create(planned.Amount, target.Currency)
                : Money.Create(planned.TargetAmount, target.Currency);

            Guid? category = null;

            if (!transfer)
            {
                IReadOnlyList<CategoryCatalog.Entry> accepting = catalog.Accepting(planned.Kind);
                category = accepting[planned.Category % accepting.Count].Key;
            }

            DateTimeOffset createdAt = createdBase.AddMinutes(planned.CreatedMinute);

            TransactionRow row = Transaction.Create(
                planned.Kind, source.Key, amount, target?.Key, targetAmount, category,
                placeKey: null, planned.OccurredOn, note: null, HistoryPlan.Today, createdAt).ToRow();

            if (planned.Deleted)
            {
                row.DeletedAtUtc = createdAt;
            }

            rows.Add(row);
        }

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            context.Accounts.AddRange(accounts);
            context.Transactions.AddRange(rows);

            await context.SaveChangesAsync();

            // Блокировка — после операций: на заблокированный счёт их не записать,
            // а на суммы она не влияет, и это тоже сверяется
            for (int i = 0; i < accounts.Count; i++)
            {
                accounts[i].IsClosed = plan.Accounts[i].Closed;
            }

            await context.SaveChangesAsync();
        }

        return new History(accounts, rows, catalog);
    }

    /// <summary>
    /// Баланс: начальный остаток, доходы со знаком плюс, расходы и списания переводов
    /// со знаком минус, зачисления переводов — плюс, в валюте счёта.
    /// </summary>
    public Money Balance(AccountRow account)
    {
        decimal total = account.OpeningBalance;

        foreach (TransactionRow row in Live)
        {
            if (row.SourceAccountKey == account.Key)
            {
                total += row.Kind is TransactionKind.Income ? row.Amount : -row.Amount;
            }

            if (row.TargetAccountKey == account.Key)
            {
                total += row.TargetAmount!.Value;
            }
        }

        return Money.Restore(total, account.Currency);
    }

    /// <summary>
    /// Лента целиком: дата, момент записи и ключ — всё по убыванию. Ключ сравнивается
    /// как текст: так он хранится и так его упорядочивает база.
    /// </summary>
    public IReadOnlyList<TransactionRow> Feed(Guid? viewed) =>
    [
        .. Live
            .Where(row => viewed is not { } account || row.SourceAccountKey == account || row.TargetAccountKey == account)
            .OrderByDescending(static row => row.OccurredOn)
            .ThenByDescending(static row => row.CreatedAtUtc)
            .ThenByDescending(static row => row.Key.ToString(), StringComparer.Ordinal)
    ];

    /// <summary>
    /// Сумма строки ленты со стороны того, кто смотрит: зачисление перевода — плюс
    /// в валюте счёта зачисления, всё остальное — со знаком вида в валюте списания.
    /// </summary>
    public Money FeedAmount(TransactionRow row, Guid? viewed)
    {
        if (row.Kind is TransactionKind.Transfer && row.TargetAccountKey == viewed)
        {
            return Money.Restore(row.TargetAmount!.Value, AccountOf(row.TargetAccountKey!.Value).Currency);
        }

        return Money.Restore(row.Kind is TransactionKind.Income ? row.Amount : -row.Amount, AccountOf(row.SourceAccountKey).Currency);
    }

    /// <summary>
    /// Итог дня. Общая лента — в рублях по нескрытым счетам; перевод входит расходом,
    /// только если ушёл на счёт вне итога. Лента счёта — в его валюте, обе стороны.
    /// </summary>
    public Money DayTotal(DateOnly day, Guid? viewed)
    {
        decimal total = 0m;

        foreach (TransactionRow row in Live.Where(row => row.OccurredOn == day))
        {
            if (viewed is { } account)
            {
                if (row.SourceAccountKey == account)
                {
                    total += row.Kind is TransactionKind.Income ? row.Amount : -row.Amount;
                }

                if (row.TargetAccountKey == account)
                {
                    total += row.TargetAmount!.Value;
                }

                continue;
            }

            if (!InDayTotals(row.SourceAccountKey))
            {
                continue;
            }

            total += row.Kind switch
            {
                TransactionKind.Income => row.Amount,
                TransactionKind.Transfer when InDayTotals(row.TargetAccountKey!.Value) => 0m,
                _ => -row.Amount
            };
        }

        Currency currency = viewed is { } key ? AccountOf(key).Currency : Currency.RUB;

        return Money.Restore(total, currency);
    }

    /// <summary>
    /// Операции, входящие в отчёт: не переводы, за месяц, по счетам набора,
    /// с категорией, у которой ни она, ни группа не стоят «вне отчётов».
    /// </summary>
    public IReadOnlyList<TransactionRow> Reported(ReportMonth month, ReportAccounts accounts) =>
    [
        .. Live.Where(row =>
            row.Kind is not TransactionKind.Transfer
            && row.OccurredOn >= month.First
            && row.OccurredOn <= month.Last
            && InReport(AccountOf(row.SourceAccountKey), accounts)
            && !Catalog[row.CategoryKey!.Value].ExcludedFromReports)
    ];

    /// <summary>
    /// Есть ли за месяц расход или доход по счёту вне набора: тогда отчёт
    /// подсказывает, что посчитано не всё. Категория тут не важна — только счёт.
    /// </summary>
    public bool HasUncounted(ReportMonth month, ReportAccounts accounts) =>
        Live.Any(row =>
            row.Kind is not TransactionKind.Transfer
            && row.OccurredOn >= month.First
            && row.OccurredOn <= month.Last
            && !InReport(AccountOf(row.SourceAccountKey), accounts));

    /// <summary>
    /// Вклад операции в строку отчёта: операция вида группы прибавляется,
    /// обратного — возврат — вычитается.
    /// </summary>
    public decimal ReportShare(TransactionRow row)
    {
        CategoryKind groupKind = Catalog[row.CategoryKey!.Value].GroupKind;
        bool own = (row.Kind is TransactionKind.Income) == (groupKind is CategoryKind.Income);

        return own ? row.Amount : -row.Amount;
    }

    private AccountRow AccountOf(Guid key) => Accounts.Single(account => account.Key == key);

    private bool InDayTotals(Guid key)
    {
        AccountRow account = AccountOf(key);

        return account.Currency is Currency.RUB && !account.ExcludedFromTotals;
    }

    private static bool InReport(AccountRow account, ReportAccounts accounts) =>
        account.Currency == accounts.Currency
        && (accounts.Keys is { } keys ? keys.Contains(account.Key) : !account.ExcludedFromTotals);
}
