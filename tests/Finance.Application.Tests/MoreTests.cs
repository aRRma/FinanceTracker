using Finance.Application.Features.More;
using Finance.Application.Features.Settings.Appearance;
using Finance.Application.Features.Settings.TimeZones;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Texts;

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

        MoreViewModel model = database.Resolve<MoreViewModel>();

        await model.LoadAsync();

        int groups = preset.Groups.Count;
        int subcategories = preset.Groups.Sum(group => group.Subcategories.Count);

        // Слова — по числу, а не одной формой: правка набора меняет числа, и форма,
        // верная для нынешних, на следующих разошлась бы с подписью
        string groupsWord = Plural.FormOf(groups, UiTexts.MoreGroupsOne, UiTexts.MoreGroupsFew, UiTexts.MoreGroupsMany);
        string subcategoriesWord = Plural.FormOf(
            subcategories, UiTexts.MoreSubcategoriesOne, UiTexts.MoreSubcategoriesFew, UiTexts.MoreSubcategoriesMany);

        Assert.Equal("0 счетов", model.AccountsCaption);
        Assert.Equal("0 мест", model.PlacesCaption);
        Assert.Equal(
            $"{groups} {groupsWord}, {subcategories} {subcategoriesWord}",
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

        MoreViewModel model = database.Resolve<MoreViewModel>();

        await model.LoadAsync();

        Assert.Equal("Как в системе", model.ThemeCaption);
        Assert.StartsWith("Как в системе · ", model.TimeZoneCaption, StringComparison.Ordinal);
        Assert.StartsWith("Версия 1.0, схема ", model.AboutCaption, StringComparison.Ordinal);

        await database.Resolve<IChangeThemeHandler>().HandleAsync(Theme.Dark);
        await model.LoadAsync();

        Assert.Equal("Тёмная", model.ThemeCaption);

        // Выбранный вручную пояс назван городом, как в списке поясов, а не идентификатором
        await database.Resolve<IChangeTimeZoneHandler>().HandleAsync("Asia/Vladivostok");
        await model.LoadAsync();

        Assert.Equal(CityNames.Of("Asia/Vladivostok"), model.TimeZoneCaption);
        Assert.NotEqual("Asia/Vladivostok", model.TimeZoneCaption);
    }

    /// <summary>
    /// Записанная операция заводит место, и подпись раздела считает его сразу.
    /// </summary>
    [Fact]
    public async Task Подпись_мест_растёт_после_первой_операции()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        MoreViewModel model = fixture.Database.Resolve<MoreViewModel>();

        Guid account = await fixture.AccountAsync("Карта");
        await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));

        await model.LoadAsync();

        Assert.Equal("1 место", model.PlacesCaption);
    }
}
