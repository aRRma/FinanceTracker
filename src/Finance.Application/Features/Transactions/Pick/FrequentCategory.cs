using Finance.Domain.Enums;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Часто используемая подкатегория для панели над списком выбора: название,
/// значок и число операций, которым её отобрали.
/// </summary>
public sealed record FrequentCategory
{
    /// <summary>
    /// Ключ подкатегории.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Название подкатегории.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Название группы — для подписи «Прочего».
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Роль подкатегории: у «Прочего» подпись с группой.
    /// </summary>
    public required CategoryRole Role { get; init; }

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Сколько операций пришлось на подкатегорию за окно частоты. На экране
    /// число не показывается: им задан только порядок чипов.
    /// </summary>
    public required int Count { get; init; }
}
