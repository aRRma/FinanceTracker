using Finance.Application.Features.More;
using Finance.Application.Features.Settings.Appearance;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Tests;

/// <summary>
/// Раздел «Ещё»: подписи справочников считают их содержимое.
/// </summary>
public sealed class MoreTests
{
    /// <summary>
    /// Подпись категорий называет оба уровня и считает весь справочник, включая
    /// «Прочее»: в справочнике эти строки видны, и вычесть их значило соврать.
    /// Мест в стартовом наборе нет вовсе — справочник наполняется сам.
    /// </summary>
    [Fact]
    public async Task Подписи_считают_счета_места_и_категории()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        Preset preset = Preset.Embedded();

        MoreViewModel model = new(
            database.Resolve<IAccountsQuery>(),
            database.Resolve<IPlacesQuery>(),
            database.Resolve<ICategoriesQuery>(),
            database.Resolve<ISettingsSummaryQuery>(),
            database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        int subcategories = preset.Groups.Sum(group => group.Subcategories.Count);

        Assert.Equal("0 счетов", model.AccountsCaption);
        Assert.Equal("0 мест", model.PlacesCaption);
        Assert.Equal(
            $"{preset.Groups.Count} групп, {subcategories} подкатегории",
            model.CategoriesCaption);
    }

    /// <summary>
    /// Строки настроек подписаны своим состоянием: по разделу видно выбранную тему
    /// и действующий пояс, не заходя в них. У системного пояса сказано и то,
    /// что он системный, — иначе после переезда подпись сменится будто сама.
    /// </summary>
    [Fact]
    public async Task Подписи_настроек_называют_тему_пояс_и_версию()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync(applicationVersion: "1.0");

        MoreViewModel model = new(
            database.Resolve<IAccountsQuery>(),
            database.Resolve<IPlacesQuery>(),
            database.Resolve<ICategoriesQuery>(),
            database.Resolve<ISettingsSummaryQuery>(),
            database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        Assert.Equal("Как в системе", model.ThemeCaption);
        Assert.StartsWith("Как в системе · ", model.TimeZoneCaption, StringComparison.Ordinal);
        Assert.StartsWith("Версия 1.0, схема ", model.AboutCaption, StringComparison.Ordinal);

        await database.Resolve<IChangeThemeHandler>().HandleAsync(Theme.Dark);
        await model.LoadAsync();

        Assert.Equal("Тёмная", model.ThemeCaption);
    }

    /// <summary>
    /// Записанная операция заводит место, и подпись раздела считает его сразу.
    /// </summary>
    [Fact]
    public async Task Подпись_мест_растёт_после_первой_операции()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        MoreViewModel model = new(
            fixture.Database.Resolve<IAccountsQuery>(),
            fixture.Database.Resolve<IPlacesQuery>(),
            fixture.Database.Resolve<ICategoriesQuery>(),
            fixture.Database.Resolve<ISettingsSummaryQuery>(),
            fixture.Database.Resolve<IChangeNotifier>());

        Guid account = await fixture.AccountAsync("Карта");
        await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));

        await model.LoadAsync();

        Assert.Equal("1 место", model.PlacesCaption);
    }
}
