using CsCheck;
using Finance.Domain.Enums;

namespace Finance.Application.Tests;

/// <summary>
/// Случайная история счетов и операций для сверки запросов с подсчётом в памяти.
/// Это план, а не записи: ключи и строки появляются при записи в базу (<see cref="History"/>).
/// </summary>
/// <remarks>
/// Генератор нарочно тянется к трудным местам: даты на границах месяцев, счета
/// в двух валютах и скрытые, переводы между ними, возвраты в универсальных группах,
/// удалённые операции и операции одного дня с одним моментом записи — там порядок
/// ленты держится только на ключе.
/// </remarks>
internal sealed record HistoryPlan(
    IReadOnlyList<HistoryPlan.AccountPlan> Accounts,
    IReadOnlyList<HistoryPlan.TransactionPlan> Transactions)
{
    /// <summary>
    /// Сегодня по тестовым часам: операций позже не бывает.
    /// </summary>
    public static readonly DateOnly Today = DateOnly.FromDateTime(TestTime.Start.UtcDateTime);

    /// <summary>
    /// Начало окна операций: с ним в окне четыре месяца и три границы между ними.
    /// </summary>
    public static readonly DateOnly First = new(2026, 6, 1);

    private static readonly DateOnly[] Boundaries =
    [
        new(2026, 6, 1), new(2026, 6, 30), new(2026, 7, 1), new(2026, 7, 31),
        new(2026, 8, 1), new(2026, 8, 31), new(2026, 9, 1), Today
    ];

    private static readonly Gen<decimal> Amount = Gen.Int[1, 500_000].Select(static kopecks => kopecks / 100m);

    private static readonly Gen<DateOnly> Day = Gen.Frequency(
        (2, Gen.OneOfConst(Boundaries)),
        (3, Gen.Int[0, Today.DayNumber - First.DayNumber].Select(static offset => First.AddDays(offset))));

    private static readonly Gen<AccountPlan> Account = Gen.Select(
        Gen.FrequencyConst((3, Currency.RUB), (1, Currency.USD)),
        Gen.Int[0, 10_000_000].Select(static kopecks => kopecks / 100m),
        Gen.Int[0, 3].Select(static roll => roll is 0),
        Gen.Int[0, 4].Select(static roll => roll is 0),
        static (currency, opening, excluded, closed) => new AccountPlan(currency, opening, excluded, closed));

    /// <summary>
    /// Генератор истории: от двух до пяти счетов и до <paramref name="maxTransactions"/> операций.
    /// </summary>
    public static Gen<HistoryPlan> Generate(int maxTransactions) =>
        from accounts in Account.Array[2, 5]
        from transactions in Transaction(accounts.Length).Array[0, maxTransactions]
        select new HistoryPlan(accounts, transactions);

    public override string ToString() =>
        $"счетов {Accounts.Count}, операций {Transactions.Count}";

    private static Gen<TransactionPlan> Transaction(int accounts) =>
        from kind in Gen.FrequencyConst(
            (5, TransactionKind.Expense), (2, TransactionKind.Income), (2, TransactionKind.Transfer))
        from source in Gen.Int[0, accounts - 1]
        from shift in Gen.Int[1, accounts - 1]
        from amount in Amount
        from targetAmount in Amount
        from category in Gen.Int[0, 999]
        from occurredOn in Day
        from minute in Gen.Int[0, 3]
        from deleted in Gen.Int[0, 9].Select(static roll => roll is 0)
        select new TransactionPlan(
            kind, source, (source + shift) % accounts, amount, targetAmount, category, occurredOn, minute, deleted);

    /// <summary>
    /// Счёт. Заблокированным он становится после записи операций — как у пользователя.
    /// </summary>
    internal sealed record AccountPlan(Currency Currency, decimal OpeningBalance, bool Excluded, bool Closed);

    /// <summary>
    /// Операция. Сумма зачисления действует только между валютами: в одной валюте
    /// она равна сумме списания. Категория — номер среди подходящих виду, по модулю.
    /// Минута записи берётся из четырёх, чтобы моменты совпадали.
    /// </summary>
    internal sealed record TransactionPlan(
        TransactionKind Kind,
        int Source,
        int Target,
        decimal Amount,
        decimal TargetAmount,
        int Category,
        DateOnly OccurredOn,
        int CreatedMinute,
        bool Deleted);
}
