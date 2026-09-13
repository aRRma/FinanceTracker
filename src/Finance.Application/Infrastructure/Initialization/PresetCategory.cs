using Finance.Domain;

namespace Finance.Application.Infrastructure.Initialization;

/// <summary>Категория набора любого уровня — для проверок, одинаковых для групп и подкатегорий.</summary>
/// <param name="Key">Устойчивый текстовый ключ.</param>
/// <param name="Id">Идентификатор, выведенный из ключа.</param>
/// <param name="Icon">Ключ значка.</param>
/// <param name="Role">Роль категории.</param>
/// <param name="ExcludeFromReports">Не показывать в отчёте.</param>
public sealed record PresetCategory(
    string Key,
    Guid Id,
    string Icon,
    CategoryRole Role,
    bool ExcludeFromReports);
