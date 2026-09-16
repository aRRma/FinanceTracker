using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Categories.Catalog;

/// <summary>
/// Строка справочника: и шапка группы, и подкатегория под ней. Один тип на оба
/// уровня, потому что список плоский: группировка списка прячет заголовок вместе
/// с содержимым, а свёрнутой группе заголовок как раз и нужен.
/// </summary>
public sealed partial class CategoryLine : ObservableObject
{
    private CategoryLine(CategoryListItem category, bool isGroup, int count)
    {
        Key = category.Key;
        Icon = category.Icon;
        Name = category.Name;
        IsGroup = isGroup;
        Count = count;
        IsProtected = category.IsProtected;
        IsService = category.Role is CategoryRole.Service;
    }

    /// <summary>
    /// Собирает шапку группы.
    /// </summary>
    /// <param name="group">Группа справочника.</param>
    /// <param name="count">Сколько в ней подкатегорий, включая «Прочее».</param>
    public static CategoryLine Group(CategoryListItem group, int count)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new CategoryLine(group, isGroup: true, count);
    }

    /// <summary>
    /// Собирает строку подкатегории.
    /// </summary>
    /// <param name="subcategory">Подкатегория.</param>
    public static CategoryLine Subcategory(CategoryListItem subcategory)
    {
        ArgumentNullException.ThrowIfNull(subcategory);

        return new CategoryLine(subcategory, isGroup: false, count: 0);
    }

    /// <summary>
    /// Ключ категории.
    /// </summary>
    public Guid Key { get; }

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Название.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Строка — шапка группы.
    /// </summary>
    public bool IsGroup { get; }

    /// <summary>
    /// Строка — подкатегория: она с отступом.
    /// </summary>
    public bool IsSubcategory => !IsGroup;

    /// <summary>
    /// Число подкатегорий в группе — плашкой у шапки.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Приёмник или служебная: такие не переносят и не удаляют.
    /// </summary>
    public bool IsProtected { get; }

    /// <summary>
    /// Служебная группа: в неё ничего не заводится.
    /// </summary>
    public bool IsService { get; }

    /// <summary>
    /// Строка открывается на правку. Приёмник и служебные — нет.
    /// </summary>
    public bool IsEditable => IsGroup ? !IsService : !IsProtected;

    /// <summary>
    /// Группа развёрнута — под шапкой видны её подкатегории.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronIcon))]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    /// Знак состояния группы: вниз — развёрнута, вправо — свёрнута.
    /// </summary>
    public string ChevronIcon => IsExpanded ? "chevron-down" : "chevron-right";
}
