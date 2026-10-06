using System.Globalization;
using Finance.Application.Features.Settings.About;
using Finance.Application.Features.Settings.Appearance;
using Finance.Application.Features.Settings.TimeZones;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Texts;

namespace Finance.Application.Tests;

/// <summary>
/// Настройки устройства: тема оформления и часовой пояс. Обе меняют не данные,
/// а то, как приложение их показывает и что считает сегодняшним днём.
/// </summary>
public sealed class SettingsTests
{
    // Две зоны по краям шкалы: между ними двадцать пять часов, поэтому
    // календарная дата в них не совпадает никогда — тест не зависит от того,
    // когда его запустили
    private const string FarEast = "Pacific/Kiritimati";
    private const string FarWest = "Pacific/Midway";

    /// <summary>
    /// Выбранная тема и сохраняется, и доходит до платформы: экран перекрашивается сразу.
    /// </summary>
    [Fact]
    public async Task Выбранная_тема_сохраняется_и_применяется()
    {
        Theme? applied = null;

        await using TestDatabase database = await TestDatabase.CreateAsync(theme => applied = theme);

        await database.Resolve<IChangeThemeHandler>().HandleAsync(Theme.Dark);

        SettingsSummary summary = await database.Resolve<ISettingsSummaryQuery>().ReadAsync();

        Assert.Equal(Theme.Dark, applied);
        Assert.Equal(Theme.Dark, summary.Theme);
    }

    /// <summary>
    /// Сохранённая тема применяется при запуске. Без этого выбор действовал бы
    /// до закрытия приложения и молча пропадал.
    /// </summary>
    [Fact]
    public async Task Сохранённая_тема_применяется_при_запуске()
    {
        Theme? applied = null;

        await using TestDatabase database = await TestDatabase.CreateAsync(theme => applied = theme);

        await database.Resolve<ILocalSettings>().SetAsync(SettingName.Theme, Theme.Light.Stored);
        await database.Resolve<FinanceStartup>().PrepareAsync();

        Assert.Equal(Theme.Light, applied);
    }

    /// <summary>
    /// Незнакомая запись настройки — системная тема. Отказ запуститься из-за
    /// испорченной строки оформления был бы несоразмерен.
    /// </summary>
    [Fact]
    public void Незнакомая_запись_темы_читается_как_системная() =>
        Assert.Equal(Theme.System, Theme.Parse("сиреневая"));

    /// <summary>
    /// Выбранный пояс меняет «сегодня» — именно ради этого настройка и заведена.
    /// </summary>
    [Fact]
    public async Task Выбранный_пояс_меняет_сегодняшнюю_дату()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        IChangeTimeZoneHandler change = database.Resolve<IChangeTimeZoneHandler>();
        IClock clock = database.Resolve<IClock>();

        await change.HandleAsync(FarEast);
        DateOnly east = clock.Today;

        await change.HandleAsync(FarWest);
        DateOnly west = clock.Today;

        Assert.NotEqual(east, west);
    }

    /// <summary>
    /// Возврат к системному поясу стирает настройку, а не записывает слово
    /// «системный»: записанное слово не переехало бы вместе с пользователем.
    /// </summary>
    [Fact]
    public async Task Возврат_к_системному_поясу_стирает_настройку()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        IChangeTimeZoneHandler change = database.Resolve<IChangeTimeZoneHandler>();
        ILocalSettings settings = database.Resolve<ILocalSettings>();

        await change.HandleAsync(FarEast);
        Assert.Equal(FarEast, await settings.GetAsync(SettingName.TimeZoneId));

        await change.HandleAsync(id: null);

        Assert.Null(await settings.GetAsync(SettingName.TimeZoneId));
        Assert.Equal(TimeZoneInfo.Local.Id, database.Resolve<IClock>().TimeZone.Id);
    }

    /// <summary>
    /// Выбранный пояс от телефона не зависит: возврат из фона его не трогает и экраны
    /// не перечитывает. «Как в системе» снова отдаёт часы зоне телефона.
    /// </summary>
    [Fact]
    public async Task Выбранный_пояс_переживает_возврат_из_фона()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        IChangeTimeZoneHandler change = database.Resolve<IChangeTimeZoneHandler>();
        SystemClock clock = database.Resolve<SystemClock>();
        IChangeNotifier changes = database.Resolve<IChangeNotifier>();

        await change.HandleAsync(FarEast);

        Assert.False(clock.FollowsSystem);

        long before = changes.VersionOf(DataChange.Settings);
        database.Resolve<TimeZoneFollower>().Resume();

        Assert.Equal(FarEast, clock.TimeZone.Id);
        Assert.Equal(before, changes.VersionOf(DataChange.Settings));

        await change.HandleAsync(id: null);

        Assert.True(clock.FollowsSystem);
        Assert.Equal(TimeZoneInfo.Local.Id, clock.TimeZone.Id);

        // Телефон пояса не менял — перечитывать экраны незачем
        before = changes.VersionOf(DataChange.Settings);
        database.Resolve<TimeZoneFollower>().Resume();

        Assert.Equal(before, changes.VersionOf(DataChange.Settings));
    }

    /// <summary>
    /// Незнакомая зона отвергается до записи. Записанная, она молча откатилась бы
    /// на системную при следующем запуске, а пользователь считал бы выбор сохранённым.
    /// </summary>
    [Fact]
    public async Task Незнакомая_зона_отвергается_и_не_записывается()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        IChangeTimeZoneHandler change = database.Resolve<IChangeTimeZoneHandler>();

        await change.HandleAsync(FarEast);

        await Assert.ThrowsAsync<TimeZoneNotFoundException>(
            () => change.HandleAsync("Europe/Атлантида"));

        Assert.Equal(FarEast, await database.Resolve<ILocalSettings>().GetAsync(SettingName.TimeZoneId));
    }

    /// <summary>
    /// Убирать нечего — не ошибка: настройка уже в том состоянии, которого от неё хотят.
    /// </summary>
    [Fact]
    public async Task Удаление_незаданной_настройки_проходит_молча()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await database.Resolve<ILocalSettings>().RemoveAsync(SettingName.TimeZoneId);

        Assert.Null(await database.Resolve<ILocalSettings>().GetAsync(SettingName.TimeZoneId));
    }

    /// <summary>
    /// «О программе» называет версию и номер схемы. Номер берётся у самой базы:
    /// по нему сверяют, накатилась ли миграция после обновления приложения.
    /// </summary>
    [Fact]
    public async Task Сводка_называет_версию_и_номер_схемы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(applicationVersion: "1.0");

        SettingsSummary summary = await database.Resolve<ISettingsSummaryQuery>().ReadAsync();

        Assert.Equal("1.0", summary.Version);
        Assert.True(summary.Schema > 0);
        Assert.True(summary.TimeZoneFromSystem);
    }

    /// <summary>
    /// Экран «О программе» показывает ту же версию и тот же номер схемы, что сводка:
    /// по номеру схемы сверяют, накатилась ли миграция после обновления.
    /// </summary>
    [Fact]
    public async Task О_программе_показывает_версию_и_схему()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(applicationVersion: "1.2.3");

        SettingsSummary summary = await database.Resolve<ISettingsSummaryQuery>().ReadAsync();

        AboutViewModel model = database.Resolve<AboutViewModel>();
        await model.LoadAsync();

        Assert.Equal("1.2.3", model.Version);
        Assert.Equal(summary.Schema.ToString(CultureInfo.InvariantCulture), model.Schema);
    }

    /// <summary>
    /// Экран оформления показывает три состояния, и выбранное отмечено ровно одно.
    /// </summary>
    [Fact]
    public async Task Экран_оформления_показывает_три_состояния()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        AppearanceViewModel model = new(
            database.Resolve<ISettingsSummaryQuery>(),
            database.Resolve<IChangeThemeHandler>(),
            database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        Assert.Equal(3, model.Options.Count);
        Assert.Single(model.Options, option => option.IsSelected);
        Assert.Equal(Theme.System, model.Current);

        await model.SelectAsync(Theme.Dark);

        Assert.Equal(Theme.Dark, model.Current);
        Assert.Single(model.Options, option => option is { IsSelected: true, Theme: Theme.Dark });
    }

    /// <summary>
    /// Список короткий: по строке на смещение, по возрастанию, подписанной городами.
    /// Первой строкой всегда стоит возврат к системному поясу — иначе после ручного
    /// выбора вернуться нечем.
    /// </summary>
    [Fact]
    public async Task Список_поясов_по_строке_на_смещение()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        TimeZoneViewModel model = database.Resolve<TimeZoneViewModel>();

        await model.LoadAsync();

        Assert.Null(model.Zones[0].Id);
        Assert.True(model.Zones[0].IsSelected);

        TimeZoneOption[] rows = [.. model.Zones.Skip(1)];
        TimeSpan[] offsets = [.. rows.Select(static row => TimeZoneInfo.FindSystemTimeZoneById(row.Id!).GetUtcOffset(TestTime.Start))];

        // Сотни зон системы сходятся в несколько десятков строк, по одной на смещение
        Assert.InRange(rows.Length, 30, 45);
        Assert.Equal(offsets.Order(), offsets);
        Assert.Equal(offsets.Length, offsets.Distinct().Count());

        // Строка ставит первую по приоритету зону своего смещения: перестановка
        // городов в списке молча сменила бы зону, которую ставит строка
        string[] expected =
        [
            .. offsets.Select(offset => TimeZoneCities.Ids.First(id =>
                TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone)
                && zone.GetUtcOffset(TestTime.Start) == offset))
        ];

        Assert.Equal(expected, rows.Select(static row => row.Id));

        // Москва ставит саму себя: города России идут в списке первыми
        TimeZoneOption moscow = Assert.Single(rows, static row => row.Id == "Europe/Moscow");

        Assert.StartsWith(CityNames.Of("Europe/Moscow"), moscow.Caption, StringComparison.Ordinal);
        Assert.Equal("UTC+3", moscow.Offset);
        Assert.Single(rows, static row => row.Offset is "UTC+5:30");
        Assert.Single(rows, static row => row.Offset is "UTC−3");
    }

    /// <summary>
    /// Выбранная строка отмечена, «Как в системе» — нет; обратный выбор возвращает отметку.
    /// </summary>
    [Fact]
    public async Task Выбранная_строка_отмечена()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        TimeZoneViewModel model = database.Resolve<TimeZoneViewModel>();

        await model.LoadAsync();
        await model.SelectAsync(model.Zones.Single(static row => row.Id == "Asia/Vladivostok"));

        Assert.False(model.IsFromSystem);
        Assert.False(model.Zones[0].IsSelected);
        Assert.Equal("Asia/Vladivostok", Assert.Single(model.Zones, static row => row.IsSelected).Id);

        await model.SelectAsync(model.Zones[0]);

        Assert.True(model.IsFromSystem);
        Assert.Same(model.Zones[0], Assert.Single(model.Zones, static row => row.IsSelected));
    }

    /// <summary>
    /// Зона, выбранная до короткого списка и в нём не оставшаяся, не теряет отметку:
    /// отмечена строка того же смещения — сегодня время у них одно.
    /// </summary>
    [Fact]
    public async Task Зона_вне_списка_отмечает_строку_своего_смещения()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        // Мидуэй в список не входит, а смещение у него то же, что у Паго-Паго
        await database.Resolve<IChangeTimeZoneHandler>().HandleAsync(FarWest);

        TimeZoneViewModel model = database.Resolve<TimeZoneViewModel>();

        await model.LoadAsync();

        Assert.DoesNotContain(FarWest, TimeZoneCities.Ids);
        Assert.Equal("Pacific/Pago_Pago", Assert.Single(model.Zones, static row => row.IsSelected).Id);
    }
}
