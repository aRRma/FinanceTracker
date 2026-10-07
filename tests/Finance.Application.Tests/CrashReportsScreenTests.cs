using System.Text.RegularExpressions;
using Finance.Application.Features.Settings.About;
using Finance.Application.Features.Settings.Diagnostics;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Texts;

namespace Finance.Application.Tests;

/// <summary>
/// Экран «Отчёты о сбоях», экран одного отчёта и строка в «О программе»: сводка, виды словами, экран
/// из следа, стек и шкала действий, файлы для отправки, очистка.
/// </summary>
public sealed class CrashReportsScreenTests : IDisposable
{
    private static readonly DeviceInfo Device = new() { AppVersion = "1.0.7", AppBuild = "10007", Android = "16", Model = "Google Pixel 7" };

    /// <summary>
    /// Зона на три часа впереди UTC: время на экране обязано отличаться от времени в файле.
    /// </summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("test+3", TimeSpan.FromHours(3), "test+3", "test+3");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "finance-crash-screen-" + Guid.NewGuid().ToString("N"));
    private readonly CrashReportChoice _choice = new();
    private readonly SystemClock _clock = new(new TestTime()) { TimeZone = Zone };

    /// <summary>
    /// Отчёты — от новых к старым, вид словом, в подписи момент и короткий тип; сверху последний сбой
    /// и счётчики: падения .NET и Java — «закрылось», остальное — «прочее».
    /// </summary>
    [Fact]
    public async Task Сводка_и_отчёты_от_новых_к_старым()
    {
        CrashReports reports = Create();
        reports.Write(CrashKind.Caught, new TimeoutException());
        reports.Write(CrashKind.Warning, new InvalidCastException());
        reports.WriteFatalJava("java.lang.IllegalStateException: x\n\tat a.b(C.java:1)");

        CrashReportsViewModel model = Model(reports);
        await model.LoadAsync();

        Assert.Equal(
            [CrashCategory.AppClosed, CrashCategory.Warning, CrashCategory.ActionFailed],
            model.Items.Select(static item => item.Category));
        Assert.Equal(
            [UiTexts.CrashCategoryAppClosed, UiTexts.CrashCategoryWarning, UiTexts.CrashCategoryActionFailed],
            model.Items.Select(static item => item.Title));
        Assert.Equal($"{UiTexts.TransactionToday}, 15:00 · IllegalStateException", model.Items[0].Caption);
        Assert.EndsWith(" · TimeoutException", model.Items[2].Caption, StringComparison.Ordinal);
        Assert.Equal([false, true, true], model.Items.Select(static item => item.HasDivider));

        Assert.Same(model.Items[0], model.Latest);
        Assert.Equal(("1", "0", "2"), (model.ClosedCount, model.FrozeCount, model.OtherCount));
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.CrashReportsAll, 3), model.AllCaption);
        Assert.True(model.HasReports);
        Assert.False(model.IsEmpty);
    }

    /// <summary>
    /// Пустое состояние — только после чтения: до него оно мигнуло бы на экране с отчётами.
    /// </summary>
    [Fact]
    public async Task Пусто_только_после_чтения()
    {
        CrashReportsViewModel model = Model(Create());

        Assert.False(model.IsEmpty);

        await model.LoadAsync();

        Assert.True(model.IsEmpty);
        Assert.False(model.HasReports);
        Assert.Null(model.Latest);
    }

    /// <summary>
    /// Прошлые завершения получают вид по причине от системы и короткую причину словами: нативное
    /// падение — сигнал, зависание — «зависло», убийство — что именно; падение без отчёта обработчика —
    /// «приложение закрылось».
    /// </summary>
    [Fact]
    public async Task Прошлое_завершение_называется_по_причине()
    {
        CrashReports reports = Create();
        reports.WriteExit(TestTime.Start.AddMinutes(-5), "exit CrashNative status 11\nsignal 11 (SIGSEGV), code 0 (SI_USER)\n#00 pc 0000000000001234  /system/lib64/libhwui.so");
        reports.WriteExit(TestTime.Start.AddMinutes(-4), "exit Anr status 0\nSubject: Input dispatching timed out");
        reports.WriteExit(TestTime.Start.AddMinutes(-3), "exit LowMemory status 0");
        reports.WriteExit(TestTime.Start.AddMinutes(-2), "exit Signaled status 9");
        reports.WriteExit(TestTime.Start.AddMinutes(-1), "exit Crash status 0");

        CrashReportsViewModel model = Model(reports);
        await model.LoadAsync();

        Assert.Equal(
            [CrashCategory.AppClosed, CrashCategory.Killed, CrashCategory.Killed, CrashCategory.Froze, CrashCategory.SystemFault],
            model.Items.Select(static item => item.Category));
        Assert.Equal(
            ["", string.Format(UiCulture.Current, UiTexts.CrashKilledSignal, 9), UiTexts.CrashKilledLowMemory, "", "SIGSEGV"],
            model.Items.Select(static item => item.Caption.Split(" · ").ElementAtOrDefault(1) ?? ""));
        Assert.Equal(("2", "1", "2"), (model.ClosedCount, model.FrozeCount, model.OtherCount));
    }

    /// <summary>
    /// Экран сбоя — из последнего перехода следа, у прошлого завершения — из сводки от системы; маршрут
    /// с вложенным экраном узнаётся по самому длинному совпадению. Служебные строки прошлого завершения
    /// в стек экрана не попадают.
    /// </summary>
    [Fact]
    public async Task Экран_берётся_из_следа_и_из_сводки()
    {
        CrashReports reports = Create();
        reports.Trail.Navigated("//report/reports/accounts");
        reports.Trail.Navigated("//more/settings/about");
        reports.Write(CrashKind.Caught, new TimeoutException());
        reports.WriteExit(TestTime.Start.AddHours(-1), "exit Anr status 0\ndescription user request after error\nscreen //report/reports/accounts | ReportAccountsPage.OnSave\nSubject: Input dispatching timed out");

        CrashReportsViewModel model = Model(reports);
        await model.LoadAsync();

        Assert.EndsWith(string.Format(UiCulture.Current, UiTexts.CrashScreen, UiTexts.AboutTitle), model.LatestCaption, StringComparison.Ordinal);

        model.Choose(model.Items[1]);
        CrashReportViewModel report = Report();
        report.Load();

        Assert.EndsWith(string.Format(UiCulture.Current, UiTexts.CrashScreen, UiTexts.ReportAccountsTitle), report.Caption, StringComparison.Ordinal);
        Assert.False(report.HasTrail);

        // Причина, описание и экран уже в шапке: в стеке остаётся только трасса
        Assert.Equal("Subject: Input dispatching timed out", report.Stack);
    }

    /// <summary>
    /// У каждого маршрута следа есть название, а незнакомый остаётся без него.
    /// </summary>
    [Fact]
    public void Незнакомый_маршрут_без_названия()
    {
        Assert.Equal(UiTexts.AccountsTitle, CrashScreens.Of("//more/accounts"));
        Assert.Equal(UiTexts.ReportAccountsTitle, CrashScreens.Of("//report/reports/accounts"));
        Assert.Equal(UiTexts.BalancesTitle, CrashScreens.Of("//balances"));
        Assert.Null(CrashScreens.Of("//more/settings/unknown"));
        Assert.Null(CrashScreens.Of("//more/xaccounts"));
    }

    /// <summary>
    /// Названия заведены ровно на маршруты приложения — вкладки каркаса и <c>Routes</c>. Новый экран без
    /// названия остался бы в отчёте без подписи, а название удалённого маршрута — мёртвая строка.
    /// </summary>
    [Fact]
    public void Названия_экранов_совпадают_с_маршрутами_приложения()
    {
        string root = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(root, "Finance.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Не найден корень репозитория");
        }

        string app = Path.Combine(root, "src", "Finance.App");
        string[] routes =
        [
            .. Regex.Matches(File.ReadAllText(Path.Combine(app, "Routes.cs")), """public const string \w+ = "(?<route>[^"]+)";""")
                .Select(static match => match.Groups["route"].Value),
            .. Regex.Matches(File.ReadAllText(Path.Combine(app, "AppShell.xaml")), """Route="(?<route>[^"]+)" """.TrimEnd())
                .Select(static match => match.Groups["route"].Value),
        ];

        Assert.True(routes.Length > 20, $"маршрутов разобрано: {routes.Length}");
        Assert.Equal(routes.Order(StringComparer.Ordinal), CrashScreens.Routes.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Экран отчёта: вид, версия и телефон по строке устройства, стек свёрнут до верхних строк и
    /// раскрывается касанием, шкала действий словами во времени пользователя, последней строкой — сбой;
    /// появление экрана, повторяющее каждый переход, в шкалу не попадает.
    /// </summary>
    [Fact]
    public async Task Отчёт_раскладывается_по_блокам()
    {
        CrashReports reports = Create();
        reports.Trail.Lifecycle(LifecycleEvent.Started);
        reports.Trail.Action("/src/Finance.App/Pages/DataPage.cs", "OnAppearing");
        reports.Trail.Navigated("//more/settings/about");
        reports.Trail.Action("/src/Finance.App/Pages/AboutPage.xaml.cs", "OnCrashReportsTapped");
        reports.WriteFatalJava(string.Join('\n', ["java.lang.IllegalStateException: Пятёрочка", .. Enumerable.Range(1, 8).Select(static line => $"\tat a.b.C.m{line}(C.java:{line})")]));

        CrashReportsViewModel list = Model(reports);
        await list.LoadAsync();
        list.Choose(list.Items[0]);

        CrashReportViewModel model = Report();
        model.Load();

        Assert.Equal((CrashCategory.AppClosed, UiTexts.CrashCategoryAppClosed), (model.Category, model.Title));
        Assert.Equal("1.0.7 (10007)", model.Version);
        Assert.Equal(("Google Pixel 7", "16"), (model.Phone, model.Android));
        Assert.Equal("IllegalStateException", model.Cause);

        Assert.True(model.CanExpandStack);
        Assert.Equal(CrashReportViewModel.PreviewLines, model.Stack.Split('\n').Length);
        Assert.Contains("9", model.StackToggle, StringComparison.Ordinal);

        model.ToggleStack();
        Assert.Equal(9, model.Stack.Split('\n').Length);
        Assert.Equal(UiTexts.CrashReportStackCollapse, model.StackToggle);

        model.ToggleStack();
        Assert.Equal(CrashReportViewModel.PreviewLines, model.Stack.Split('\n').Length);

        Assert.Equal(
            [UiTexts.CrashTrailStarted, string.Format(UiCulture.Current, UiTexts.CrashScreen, UiTexts.AboutTitle), "AboutPage.OnCrashReportsTapped", UiTexts.CrashTrailCrash],
            model.Trail.Select(static entry => entry.Text));
        Assert.Equal([false, false, true, false], model.Trail.Select(static entry => entry.IsCode));
        Assert.True(model.Trail[^1].IsCrash);

        // TestTime — полдень UTC, на экране — на три часа позже
        Assert.All(model.Trail, static entry => Assert.StartsWith("15:00:00", entry.Time, StringComparison.Ordinal));
    }

    /// <summary>
    /// Составное исключение — обёртка: строка называет первое вложенное, а не саму обёртку.
    /// </summary>
    [Fact]
    public async Task Составное_исключение_называется_вложенным()
    {
        CrashReports reports = Create();
        reports.Write(CrashKind.Warning, new AggregateException(new TimeoutException()));

        CrashReportsViewModel model = Model(reports);
        await model.LoadAsync();

        Assert.EndsWith(" · TimeoutException", Assert.Single(model.Items).Caption, StringComparison.Ordinal);
    }

    /// <summary>
    /// Короткое имя типа — без пространства имён и без внешнего класса; текст после двоеточия отрезается.
    /// </summary>
    [Theory]
    [InlineData("android.app.RemoteServiceException$CrashedByAdbException", "CrashedByAdbException")]
    [InlineData("System.TimeoutException", "TimeoutException")]
    [InlineData("Finance.Application.Infrastructure.Storage.DatabaseMigrationException: Миграция не удалась", "DatabaseMigrationException")]
    [InlineData("Plain", "Plain")]
    public void Короткое_имя_типа(string headline, string expected) =>
        Assert.Equal(expected, CrashReportSummary.ShortType(headline));

    /// <summary>
    /// Для «Поделиться» оба файла сводятся в один — вместе с теми отчётами, что уже уехали в предыдущий.
    /// </summary>
    [Fact]
    public async Task Файл_для_отправки_сводит_оба_файла()
    {
        CrashReports reports = Create(fileLimit: 1024);

        for (int index = 0; index < 20; index++)
        {
            reports.Write(CrashKind.Caught, new TimeoutException());
        }

        Assert.True(File.Exists(reports.PreviousPath));

        string file = await Model(reports).PrepareShareAsync(Path.Combine(_folder, "cache"));

        Assert.Equal(
            File.ReadAllText(reports.PreviousPath) + File.ReadAllText(reports.CurrentPath),
            File.ReadAllText(file));
    }

    /// <summary>
    /// С экрана отчёта уходит только этот отчёт, и файл читается тем же разбором, что и файл отчётов.
    /// </summary>
    [Fact]
    public async Task С_экрана_отчёта_уходит_один_отчёт()
    {
        CrashReports reports = Create();
        reports.Write(CrashKind.Caught, new TimeoutException());
        reports.Write(CrashKind.Warning, new InvalidCastException());

        CrashReportsViewModel list = Model(reports);
        await list.LoadAsync();
        list.Choose(list.Items[1]);

        CrashReportViewModel model = Report();
        model.Load();
        string? file = await model.PrepareShareAsync(Path.Combine(_folder, "cache"));

        CrashReport shared = Assert.Single(CrashReportFile.Parse(File.ReadAllText(file!)));
        Assert.Equal((list.Items[1].Report.AtUtc, CrashKind.Caught), (shared.AtUtc, shared.Kind));
        Assert.Equal(list.Items[1].Report.Text, shared.Text);
    }

    /// <summary>
    /// Предыдущий файл кончается оборванной записью — в сведённом файле первый отчёт текущего всё равно цел.
    /// </summary>
    [Fact]
    public async Task Оборванный_хвост_предыдущего_файла_не_съедает_отчёт_текущего()
    {
        CrashReports reports = Create(fileLimit: 1024);

        for (int index = 0; index < 20; index++)
        {
            reports.Write(CrashKind.Caught, new TimeoutException());
        }

        File.AppendAllText(reports.PreviousPath, "=== 2026-09-15T00:00:00.0000000+00:00 Caught\napp");
        string shared = Path.Combine(_folder, "shared.txt");
        reports.CopyTo(shared);

        // Склеенный отчёт пропадает не числом: его тело достаётся оборванному заголовку, так что сверяются моменты
        Assert.Equal(
            (await reports.ReadAsync(CancellationToken.None)).Select(static report => report.AtUtc).Order(),
            CrashReportFile.Parse(File.ReadAllText(shared)).Select(static report => report.AtUtc).Order());
    }

    /// <summary>
    /// Очистка убирает отчёты с экрана, из файла и обе копии для «Поделиться» — сведённую и одного
    /// отчёта, даже если их готовили другие модели; строка в «О программе» показывает ноль.
    /// </summary>
    [Fact]
    public async Task Очистка_убирает_всё_и_строка_показывает_ноль()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Resolve<CrashReports>().Write(CrashKind.Caught, new TimeoutException());
        string cache = database.Location.CacheFolder;

        AboutViewModel about = database.Resolve<AboutViewModel>();
        await about.LoadAsync();
        Assert.Equal("1", about.CrashReportCount);

        CrashReportsViewModel first = database.Resolve<CrashReportsViewModel>();
        await first.LoadAsync();
        string shared = await first.PrepareShareAsync(cache);
        first.Choose(first.Items[0]);
        CrashReportViewModel report = database.Resolve<CrashReportViewModel>();
        report.Load();
        string? single = await report.PrepareShareAsync(cache);
        Assert.True(File.Exists(shared) && File.Exists(single));

        CrashReportsViewModel model = database.Resolve<CrashReportsViewModel>();
        await model.LoadAsync();
        await model.ClearAsync(cache);

        Assert.Empty(model.Items);
        Assert.True(model.IsEmpty);
        Assert.False(File.Exists(shared));
        Assert.False(File.Exists(single));
        Assert.Empty(await database.Resolve<CrashReports>().ReadAsync(CancellationToken.None));

        await about.LoadAsync();
        Assert.Equal("0", about.CrashReportCount);
    }

    /// <summary>
    /// Нечитаемый файл отчётов не уносит «О программе»: версия и схема на месте, число — пустое.
    /// </summary>
    [Fact]
    public async Task Нечитаемые_отчёты_не_ломают_о_программе()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        CrashReports reports = database.Resolve<CrashReports>();
        reports.Write(CrashKind.Caught, new TimeoutException());

        AboutViewModel about = database.Resolve<AboutViewModel>();

        // Файл открыт без права на совместное чтение: чтение отчётов упадёт с IOException
        using (new FileStream(reports.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await about.LoadAsync();
        }

        Assert.NotEmpty(about.Version);
        Assert.NotEmpty(about.Schema);
        Assert.Empty(about.CrashReportCount);
    }

    /// <summary>
    /// Момент — в зоне пользователя: сегодня и вчера словом, иначе дата, год — только чужой.
    /// </summary>
    [Fact]
    public void Момент_в_зоне_пользователя_сегодня_и_вчера_словом()
    {
        DateTimeOffset moment = new(2025, 12, 31, 22, 5, 0, TimeSpan.Zero);

        Assert.Equal($"{UiTexts.TransactionToday}, 01:05", DateText.Moment(moment, Zone, new DateOnly(2026, 1, 1)));
        Assert.Equal($"{UiTexts.TransactionYesterday}, 01:05", DateText.Moment(moment, Zone, new DateOnly(2026, 1, 2)));
        Assert.Equal("1 января, 01:05", DateText.Moment(moment, Zone, new DateOnly(2026, 9, 15)));
        Assert.Equal("1 января 2026, 01:05", DateText.Moment(moment, Zone, new DateOnly(2027, 1, 2)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private CrashReports Create(int fileLimit = CrashReports.DefaultFileLimit) =>
        new(Path.Combine(_folder, "data"), new TestTime(), Device, fileLimit);

    private CrashReportsViewModel Model(CrashReports reports) => new(reports, _choice, _clock);

    private CrashReportViewModel Report() => new(_choice, _clock);
}
