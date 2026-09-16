using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure.Initialization;

/// <summary>
/// Подкатегория стартового набора.
/// </summary>
/// <param name="Key">Устойчивый текстовый ключ вида <c>группа.подкатегория</c>.</param>
/// <param name="Name">Название подкатегории.</param>
/// <param name="Icon">Ключ значка.</param>
/// <param name="Role">Обычная, приёмник группы или служебная.</param>
/// <param name="Id">Идентификатор, выведенный из пространства имён и ключа.</param>
/// <param name="ExcludeFromReports">Не показывать в отчёте.</param>
public sealed record PresetSubcategory(
    string Key,
    string Name,
    string Icon,
    CategoryRole Role,
    Guid Id,
    bool ExcludeFromReports = false);
