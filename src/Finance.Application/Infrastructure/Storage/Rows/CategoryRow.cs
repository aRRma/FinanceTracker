using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure.Storage.Rows;

/// <summary>
/// Строка таблицы категорий. Уровень задаётся заполненностью <see cref="ParentKey"/>:
/// отдельной колонки уровня нет, два источника одного факта разошлись бы.
/// </summary>
internal sealed class CategoryRow : EntityRow
{
    /// <summary>
    /// Ключ группы. Пуст у группы, заполнен у подкатегории.
    /// </summary>
    public Guid? ParentKey { get; set; }

    /// <summary>
    /// Вид. Задан только у группы; подкатегория наследует его от неё.
    /// </summary>
    public CategoryKind? Kind { get; set; }

    /// <summary>
    /// Название категории.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Ключ значка из набора, зашитого в приложение.
    /// </summary>
    public required string Icon { get; set; }

    /// <summary>
    /// Обычная, приёмник группы или служебная.
    /// </summary>
    public required CategoryRole Role { get; set; }

    /// <summary>
    /// Не показывать в отчёте и не включать в его суммы.
    /// </summary>
    public required bool ExcludeFromReports { get; set; }
}
