using Finance.Application.Infrastructure;
using Finance.Domain;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Счёт в списке выбора формы. Баланс показан прямо в списке: выбирая счёт
/// для расхода, обычно и хотят знать, хватит ли на нём денег.
/// </summary>
/// <param name="Key">Ключ счёта.</param>
/// <param name="Name">Наименование.</param>
/// <param name="Currency">Валюта — вместе со счётом меняется валюта суммы.</param>
/// <param name="Balance">Баланс на сейчас.</param>
/// <param name="OpenedOn">Дата открытия — раньше неё дата операции недоступна.</param>
/// <param name="IsClosed">Счёт закрыт: в выборе не предлагается, но в уже записанной операции остаётся.</param>
public sealed record AccountOption(
    Guid Key,
    string Name,
    Currency Currency,
    Money Balance,
    DateOnly OpenedOn,
    bool IsClosed)
{
    /// <summary>Подпись строки выбора: название и баланс.</summary>
    public string Label => $"{Name} · {Balance.Display}";
}
