namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Всё, из чего форма операции даёт выбирать: счета, подкатегории и последний
/// использованный счёт. Читается одним запросом при открытии формы. Мест здесь
/// нет: их читает свой экран выбора, и форме держать в памяти весь справочник
/// ради одной строки незачем.
/// </summary>
public sealed record TransactionForm
{
    /// <summary>
    /// Все счета, включая заблокированные: заблокированный нужен, когда правится его старая операция.
    /// </summary>
    public required IReadOnlyList<AccountOption> Accounts { get; init; }

    /// <summary>
    /// Подкатегории обоих видов в порядке групп.
    /// </summary>
    public required IReadOnlyList<CategoryOption> Categories { get; init; }

    /// <summary>
    /// Счёт, подставляемый в новую операцию. Пусто — операций ещё не было.
    /// </summary>
    public required Guid? LastAccountKey { get; init; }
}
