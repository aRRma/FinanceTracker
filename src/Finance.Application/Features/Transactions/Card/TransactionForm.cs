namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Всё, из чего форма операции даёт выбирать: счета, подкатегории, места
/// и последний использованный счёт. Читается одним запросом при открытии формы.
/// </summary>
public sealed record TransactionForm
{
    /// <summary>Все счета, включая закрытые: закрытый нужен, когда правится его старая операция.</summary>
    public required IReadOnlyList<AccountOption> Accounts { get; init; }

    /// <summary>Подкатегории обоих видов в порядке групп.</summary>
    public required IReadOnlyList<CategoryOption> Categories { get; init; }

    /// <summary>Места от частых к редким.</summary>
    public required IReadOnlyList<PlaceOption> Places { get; init; }

    /// <summary>Счёт, подставляемый в новую операцию. Пусто — операций ещё не было.</summary>
    public required Guid? LastAccountKey { get; init; }
}
