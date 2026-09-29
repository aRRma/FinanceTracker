using Finance.Application.Features.Categories.Card;
using Finance.Domain.Enums;

namespace Finance.Application.Tests;

/// <summary>
/// Команды категорий для тестов справочника. Подключается через <c>using static</c>.
/// </summary>
internal static class CategorySetup
{
    public static SaveCategoryCommand Group(string name, CategoryKind kind) =>
        new() { Name = name, Icon = "basket", Kind = kind };

    public static SaveCategoryCommand Subcategory(Guid parentKey, string name) =>
        new() { ParentKey = parentKey, Name = name, Icon = "basket" };

    public static Task<Guid> SaveAsync(TestDatabase database, SaveCategoryCommand command) =>
        database.Resolve<ISaveCategoryHandler>().HandleAsync(command);
}
