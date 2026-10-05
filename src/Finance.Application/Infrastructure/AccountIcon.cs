using System.Collections.Frozen;
using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Значок счёта в списках. Живёт в общей инфраструктуре, а не в слайсе: один
/// и тот же значок обязаны показать балансы, справочник счетов, лента и выбор счёта
/// в форме операции, а разойдись они — один и тот же счёт выглядел бы на разных
/// экранах по-разному.
/// </summary>
public static class AccountIcon
{
    /// <summary>
    /// Значки, из которых выбирают значок счёта, в порядке сетки выбора. Набор свой,
    /// а не набор категорий: счёту нужны знаки назначения — банк, отпуск, дом, —
    /// а не покупок. Контур каждого лежит в общем файле контуров.
    /// </summary>
    public static IReadOnlyList<string> Choices { get; } =
    [
        "credit-card", "cash", "building-bank", "wallet", "coins", "trending-up",
        "percentage", "device-mobile", "plane", "beach", "home", "heart-handshake",
        "gift", "shield", "key", "car", "school", "users"
    ];

    private static readonly FrozenSet<string> Known = Choices.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Ключ значка для счёта. Выбранный руками значок сильнее всего; без него —
    /// по типу, и признак «скрытый» сильнее типа: у накоплений значок свой, а не
    /// карты или наличных, — так их видно в списке сразу. Неизвестный ключ
    /// ошибкой не считается и рисуется значком по типу.
    /// </summary>
    /// <param name="type">Тип счёта.</param>
    /// <param name="excludedFromTotals">Счёт скрыт — это накопления.</param>
    /// <param name="chosen">Значок, выбранный руками; пусто — по типу.</param>
    public static string For(AccountType type, bool excludedFromTotals, string? chosen) =>
        chosen is not null && Known.Contains(chosen) ? chosen : ByType(type, excludedFromTotals);

    /// <summary>
    /// Значок по типу — тот, что стоит, пока значок не выбран руками.
    /// </summary>
    /// <param name="type">Тип счёта.</param>
    /// <param name="excludedFromTotals">Счёт скрыт — это накопления.</param>
    public static string ByType(AccountType type, bool excludedFromTotals) => (type, excludedFromTotals) switch
    {
        (_, true) => "building-bank",
        (AccountType.Cash, _) => "cash",
        _ => "credit-card"
    };
}
