using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Значок счёта в списках. Живёт в общей инфраструктуре, а не в слайсе: один
/// и тот же значок обязаны показать балансы, справочник счетов и выбор счёта
/// в форме операции, а разойдись они — один и тот же счёт выглядел бы на трёх
/// экранах по-разному.
/// </summary>
public static class AccountIcon
{
    /// <summary>
    /// Ключ значка для счёта. Признак «скрыть из расчётов» сильнее типа: у накоплений
    /// значок свой, а не карты или наличных, — так их видно в списке сразу.
    /// </summary>
    /// <param name="type">Тип счёта.</param>
    /// <param name="excludedFromTotals">Счёт скрыт из расчётов — это накопления.</param>
    public static string For(AccountType type, bool excludedFromTotals) => (type, excludedFromTotals) switch
    {
        (_, true) => "building-bank",
        (AccountType.Cash, _) => "cash",
        _ => "credit-card"
    };
}
