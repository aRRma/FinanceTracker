using Finance.Domain;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Категория в списке — в справочнике, в карточке группы и в подписи раздела «Ещё».
/// Плоская модель чтения: оба уровня одной записью, уровень виден по <see cref="ParentKey"/>.
/// </summary>
public sealed record CategoryListItem
{
    /// <summary>Ключ категории.</summary>
    public required Guid Key { get; init; }

    /// <summary>Ключ группы. Пуст у группы — этим уровень и задаётся.</summary>
    public required Guid? ParentKey { get; init; }

    /// <summary>
    /// Вид. У подкатегории подставлен от её группы: экран отбирает по виду обе
    /// строки одинаково, а хранится он только у первого уровня.
    /// </summary>
    public required CategoryKind Kind { get; init; }

    /// <summary>Название категории.</summary>
    public required string Name { get; init; }

    /// <summary>Ключ значка. Неизвестный рисуется запасным, ошибкой это не считается.</summary>
    public required string Icon { get; init; }

    /// <summary>Роль: обычная, приёмник «Прочее» или служебная.</summary>
    public required CategoryRole Role { get; init; }

    /// <summary>Категория — группа первого уровня.</summary>
    public bool IsGroup => ParentKey is null;

    /// <summary>Категория не удаляется и не переносится: приёмник или служебная.</summary>
    public bool IsProtected => Role is CategoryRole.Other or CategoryRole.Service;
}
