using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Categories.Catalog;

/// <summary>
/// Строка подкатегории в справочнике и в карточке группы. Показывать её и
/// открывать — разные вопросы: «Прочее» и служебная видны, но не открываются.
/// </summary>
/// <param name="Key">Ключ подкатегории.</param>
/// <param name="Name">Название.</param>
/// <param name="Icon">Ключ значка.</param>
/// <param name="IsProtected">Не открывается и не удаляется: приёмник или служебная.</param>
public sealed record CategoryRowItem(Guid Key, string Name, string Icon, bool IsProtected)
{
    /// <summary>Строка открывается по нажатию.</summary>
    public bool IsEditable => !IsProtected;

    /// <summary>Собирает строку из модели чтения.</summary>
    /// <param name="category">Категория из справочника.</param>
    public static CategoryRowItem From(CategoryListItem category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new CategoryRowItem(category.Key, category.Name, category.Icon, category.IsProtected);
    }
}
