using Finance.Application.Infrastructure;
using Finance.Domain;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Счёт в списке выбора формы. Баланс показан прямо в списке: выбирая счёт
/// для расхода, обычно и хотят знать, хватит ли на нём денег.
/// </summary>
/// <param name="Key">Ключ счёта.</param>
/// <param name="Icon">Ключ значка: наличные, карта или накопления.</param>
/// <param name="Name">Наименование.</param>
/// <param name="Currency">Валюта — вместе со счётом меняется валюта суммы.</param>
/// <param name="Balance">Баланс на сейчас.</param>
/// <param name="OpenedOn">Дата открытия — раньше неё дата операции недоступна.</param>
/// <param name="IsClosed">Счёт закрыт: в выборе не предлагается, но в уже записанной операции остаётся.</param>
/// <param name="IsSavings">Счёт скрыт из расчётов: в выборе стоит отдельным разделом «Накопления».</param>
public sealed record AccountOption(
    Guid Key,
    string Icon,
    string Name,
    Currency Currency,
    Money Balance,
    DateOnly OpenedOn,
    bool IsClosed,
    bool IsSavings)
{
    /// <summary>Подпись строки выбора: название и баланс.</summary>
    public string Label => $"{Name} · {Balance.Display}";
}
