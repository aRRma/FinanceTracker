using Finance.Application.Features.Settings.About;
using Finance.Application.Features.Settings.Diagnostics;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Texts;

namespace Finance.Application.Tests;

/// <summary>
/// Экран «Отчёты о сбоях» и его строка в «О программе»: список, пустое состояние, раскрытие, файл для отправки, очистка.
/// </summary>
public sealed class CrashReportsScreenTests
{
    /// <summary>
    /// Отчёты — от новых к старым, вид словом, в подписи момент и первая строка; падение .NET и Java — одно слово.
    /// </summary>
    [Fact]
    public async Task Отчёты_от_новых_к_старым_с_видом_словом()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        CrashReports reports = database.Resolve<CrashReports>();

        reports.Write(CrashKind.Caught, new TimeoutException());
        reports.Write(CrashKind.Warning, new InvalidCastException());
        reports.WriteFatalJava("java.lang.IllegalStateException: x\n\tat a.b(C.java:1)");

        CrashReportsViewModel model = database.Resolve<CrashReportsViewModel>();
        await model.LoadAsync();

        Assert.Equal(
            [UiTexts.CrashKindFatal, UiTexts.CrashKindWarning, UiTexts.CrashKindCaught],
            model.Items.Select(static item => item.Kind));
        Assert.EndsWith(" · java.lang.IllegalStateException", model.Items[0].Caption, StringComparison.Ordinal);
        Assert.StartsWith("15 сентября, ", model.Items[2].Caption, StringComparison.Ordinal);
        Assert.Contains("System.TimeoutException", model.Items[2].Text, StringComparison.Ordinal);
        Assert.True(model.HasReports);
        Assert.False(model.IsEmpty);
    }

    /// <summary>
    /// Пустое состояние — только после чтения: до него оно мигнуло бы на экране с отчётами.
    /// </summary>
    [Fact]
    public async Task Пусто_только_после_чтения()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        CrashReportsViewModel model = database.Resolve<CrashReportsViewModel>();

        Assert.False(model.IsEmpty);

        await model.LoadAsync();

        Assert.True(model.IsEmpty);
        Assert.False(model.HasReports);
    }

    /// <summary>
    /// Касание раскрывает отчёт, второе — сворачивает.
    /// </summary>
    [Fact]
    public async Task Касание_раскрывает_и_сворачивает()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Resolve<CrashReports>().Write(CrashKind.Caught, new TimeoutException());
        CrashReportsViewModel model = database.Resolve<CrashReportsViewModel>();
        await model.LoadAsync();

        CrashReportItem item = Assert.Single(model.Items);
        Assert.False(item.IsExpanded);

        CrashReportsViewModel.Toggle(item);
        Assert.True(item.IsExpanded);

        CrashReportsViewModel.Toggle(item);
        Assert.False(item.IsExpanded);
    }

    /// <summary>
    /// Для «Поделиться» оба файла сводятся в один — вместе с теми отчётами, что уже уехали в предыдущий.
    /// </summary>
    [Fact]
    public async Task Файл_для_отправки_сводит_оба_файла()
    {
        string folder = Path.Combine(Path.GetTempPath(), "finance-share-" + Guid.NewGuid().ToString("N"));

        try
        {
            CrashReports reports = new(Path.Combine(folder, "data"), new TestTime(), DeviceInfo.Unknown, fileLimit: 1024);

            for (int index = 0; index < 20; index++)
            {
                reports.Write(CrashKind.Caught, new TimeoutException());
            }

            Assert.True(File.Exists(reports.PreviousPath));

            CrashReportsViewModel model = new(reports, new SystemClock(new TestTime()));
            string file = await model.PrepareShareAsync(Path.Combine(folder, "cache"));

            Assert.Equal(
                File.ReadAllText(reports.PreviousPath) + File.ReadAllText(reports.CurrentPath),
                File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Очистка убирает отчёты с экрана, из файла и копию, сведённую для «Поделиться» — даже если её
    /// готовила другая модель экрана; строка в «О программе» показывает ноль.
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

        string shared = await database.Resolve<CrashReportsViewModel>().PrepareShareAsync(cache);
        Assert.True(File.Exists(shared));

        CrashReportsViewModel model = database.Resolve<CrashReportsViewModel>();
        await model.LoadAsync();
        await model.ClearAsync(cache);

        Assert.Empty(model.Items);
        Assert.True(model.IsEmpty);
        Assert.False(File.Exists(shared));
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
    /// Момент — в зоне пользователя, год — только чужой.
    /// </summary>
    [Fact]
    public void Момент_в_зоне_пользователя_и_год_только_чужой()
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("test+3", TimeSpan.FromHours(3), "test+3", "test+3");
        DateTimeOffset moment = new(2025, 12, 31, 22, 5, 0, TimeSpan.Zero);

        Assert.Equal("1 января, 01:05", DateText.Moment(moment, zone, new DateOnly(2026, 9, 15)));
        Assert.Equal("1 января 2026, 01:05", DateText.Moment(moment, zone, new DateOnly(2027, 1, 2)));
    }
}
