using System.Collections.ObjectModel;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Categories.Catalog;

/// <summary>
/// Группа в справочнике: шапка с названием, значком и числом подкатегорий, под ней
/// строки. Наследует коллекцию, а не содержит её, — так группу понимает список
/// с группировкой.
/// </summary>
public sealed class CategoryGroupItem : ObservableCollection<CategoryRowItem>
{
    /// <summary>Создаёт группу справочника.</summary>
    /// <param name="group">Группа из справочника.</param>
    /// <param name="subcategories">Её подкатегории в порядке показа.</param>
    public CategoryGroupItem(CategoryListItem group, IEnumerable<CategoryRowItem> subcategories)
        : base([.. subcategories])
    {
        ArgumentNullException.ThrowIfNull(group);

        Key = group.Key;
        Name = group.Name;
        Icon = group.Icon;
        IsService = group.Role is CategoryRole.Service;

        // Считается и «Прочее»: подпись раздела «Ещё» берёт то же число
        // по всему справочнику, и вычесть приёмник здесь значило развести их
        SubcategoryCount = Count;
    }

    /// <summary>Ключ группы.</summary>
    public Guid Key { get; }

    /// <summary>Название группы.</summary>
    public string Name { get; }

    /// <summary>Ключ значка группы.</summary>
    public string Icon { get; }

    /// <summary>Сколько в группе подкатегорий — число рядом с названием.</summary>
    public int SubcategoryCount { get; }

    /// <summary>Группа служебная: в неё ничего не заводится и не переносится.</summary>
    public bool IsService { get; }
}
