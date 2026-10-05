namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Всё, из чего форма операции даёт выбирать: счета, подкатегории и счёт
/// по умолчанию. Читается одним запросом при открытии формы. Мест здесь
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
    /// Счёт по умолчанию — подставляется в новую операцию. Пусто — незаблокированных счетов нет.
    /// </summary>
    public required Guid? DefaultAccountKey { get; init; }
}
