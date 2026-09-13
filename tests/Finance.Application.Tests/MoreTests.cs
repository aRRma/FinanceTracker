using Finance.Application.Features.More;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Tests;

/// <summary>Раздел «Ещё»: подписи справочников считают их содержимое.</summary>
public sealed class MoreTests
{
    /// <summary>
    /// Подпись категорий называет оба уровня и считает весь справочник, включая
    /// «Прочее»: в справочнике эти строки видны, и вычесть их значило соврать.
    /// </summary>
    [Fact]
    public async Task Подписи_считают_счета_и_категории()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        Preset preset = Preset.Embedded();

        MoreViewModel model = new(
            database.Resolve<IAccountsQuery>(),
            database.Resolve<ICategoriesQuery>(),
            database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        int subcategories = preset.Groups.Sum(group => group.Subcategories.Count);

        Assert.Equal("0 счетов", model.AccountsCaption);
        Assert.Equal(
            $"{preset.Groups.Count} групп, {subcategories} подкатегории",
            model.CategoriesCaption);
    }
}
