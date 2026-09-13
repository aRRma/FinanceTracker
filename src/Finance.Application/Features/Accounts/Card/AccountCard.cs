using Finance.Domain;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Счёт, как он показан в карточке. Кроме самих полей несёт то, что экран обязан
/// показать заранее: валюта заперта операциями, а дату открытия дальше определённой
/// сдвинуть нельзя. Узнать это при сохранении поздно — пользователь уже всё ввёл.
/// </summary>
public sealed record AccountCard
{
    /// <summary>Ключ счёта.</summary>
    public required Guid Key { get; init; }

    /// <summary>Наименование счёта.</summary>
    public required string Name { get; init; }

    /// <summary>Наличные или карта.</summary>
    public required AccountType Type { get; init; }

    /// <summary>Валюта счёта.</summary>
    public required Currency Currency { get; init; }

    /// <summary>Начальный остаток.</summary>
    public required decimal OpeningBalance { get; init; }

    /// <summary>Дата открытия.</summary>
    public required DateOnly OpenedOn { get; init; }

    /// <summary>«Скрыть из расчётов».</summary>
    public required bool ExcludedFromTotals { get; init; }

    /// <summary>«Счёт закрыт».</summary>
    public required bool IsClosed { get; init; }

    /// <summary>Валюту менять нельзя: по счёту уже была операция, пусть и удалённая.</summary>
    public required bool CurrencyLocked { get; init; }

    /// <summary>Дата самой ранней операции по счёту — дальше неё открытие не сдвигается.</summary>
    public required DateOnly? EarliestTransactionOn { get; init; }
}
