using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure.Initialization;

/// <summary>
/// Группа стартового набора.
/// </summary>
/// <param name="Key">Устойчивый текстовый ключ. Из него выводится идентификатор, менять нельзя.</param>
/// <param name="Name">Название группы.</param>
/// <param name="Kind">Доход или расход. Подкатегории наследуют.</param>
/// <param name="Icon">Ключ значка из набора, зашитого в приложение.</param>
/// <param name="Role">Обычная или служебная.</param>
/// <param name="Id">Идентификатор, выведенный из пространства имён и ключа.</param>
/// <param name="Subcategories">Подкатегории группы.</param>
/// <param name="ExcludeFromReports">Не показывать в отчёте.</param>
/// <param name="AcceptsAnyKind">Принимать операции обоих видов, а не только своего.</param>
public sealed record PresetGroup(
    string Key,
    string Name,
    CategoryKind Kind,
    string Icon,
    CategoryRole Role,
    Guid Id,
    IReadOnlyList<PresetSubcategory> Subcategories,
    bool ExcludeFromReports = false,
    bool AcceptsAnyKind = false);
