using System.Text;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Domain.Errors;

namespace Finance.Application.Tests;

/// <summary>
/// След действий: порядок, вытеснение старых, отрезание параметров маршрута, сводка для системы и попадание в отчёт.
/// </summary>
public sealed class ActionTrailTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "finance-trail-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// События читаются от старых к новым, у каждого — момент, вид и имя из кода.
    /// </summary>
    [Fact]
    public void События_читаются_по_порядку()
    {
        ActionTrail trail = new(new TestTime());

        trail.Lifecycle(LifecycleEvent.Started);
        trail.Navigated("//balances");
        trail.Action(@"C:\src\Finance.App\Pages\TransactionPage.xaml.cs", "OnSaveClicked");
        trail.RuleBroken(Invariant.NameUnique);

        Assert.Equal(
            ["12:00:00.001 life Started", "12:00:00.002 nav //balances", "12:00:00.003 run TransactionPage.OnSaveClicked", "12:00:00.004 rule NameUnique"],
            trail.Snapshot());
    }

    /// <summary>
    /// Хранятся только последние события: старые вытесняются по одному.
    /// </summary>
    [Fact]
    public void Старые_события_вытесняются()
    {
        ActionTrail trail = new(new TestTime());

        for (int number = 1; number <= ActionTrail.Capacity + 5; number++)
        {
            trail.Navigated($"//screen{number}");
        }

        IReadOnlyList<string> events = trail.Snapshot();
        Assert.Equal(ActionTrail.Capacity, events.Count);
        Assert.EndsWith("//screen6", events[0], StringComparison.Ordinal);
        Assert.EndsWith($"//screen{ActionTrail.Capacity + 5}", events[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Параметры маршрута отрезаются: в них ключ записи и вид операции.
    /// </summary>
    [Fact]
    public void Параметры_маршрута_отрезаются()
    {
        ActionTrail trail = new(new TestTime());

        trail.Navigated("//balances/transaction?key=0199a3b2-7c1e-7d40-9b1a-2f4c5d6e7f80&kind=Expense");

        string entry = Assert.Single(trail.Snapshot());
        Assert.EndsWith(" nav //balances/transaction", entry, StringComparison.Ordinal);
    }

    /// <summary>
    /// От пути к файлу остаётся имя без расширения — и с разделителями Windows, и с разделителями Linux.
    /// </summary>
    [Theory]
    [InlineData(@"C:\src\Finance.App\Pages\FeedPage.xaml.cs")]
    [InlineData("/home/runner/work/src/Finance.App/Pages/FeedPage.xaml.cs")]
    [InlineData("FeedPage.cs")]
    public void От_пути_остаётся_имя_файла(string file)
    {
        ActionTrail trail = new(new TestTime());

        trail.Action(file, "OnRefreshing");

        Assert.EndsWith(" run FeedPage.OnRefreshing", Assert.Single(trail.Snapshot()), StringComparison.Ordinal);
    }

    /// <summary>
    /// Сводка — текущий экран и последнее действие, а не последнее событие: уход в фон её не стирает.
    /// </summary>
    [Fact]
    public void Сводка_держит_экран_и_последнее_действие()
    {
        ActionTrail trail = new(new TestTime());
        int changes = 0;
        trail.Changed += () => changes++;

        trail.Navigated("//balances/transaction?kind=Expense");
        trail.Action("TransactionPage.xaml.cs", "OnSaveClicked");
        trail.Lifecycle(LifecycleEvent.Stopped);

        Assert.Equal("//balances/transaction | TransactionPage.OnSaveClicked", Encoding.UTF8.GetString(trail.Summary()));
        Assert.Equal(2, changes);
    }

    /// <summary>
    /// Сводка режется по пределу системы в байтах UTF-8 и не разрывает многобайтный символ.
    /// </summary>
    [Fact]
    public void Сводка_режется_по_байтам_не_разрывая_символ()
    {
        ActionTrail trail = new(new TestTime());

        // Кириллица — по два байта: 127 байт ascii-части плюс двухбайтная буква не влезают в 128
        trail.Navigated(new string('a', 127) + "ж");

        byte[] summary = trail.Summary();
        Assert.True(summary.Length <= ActionTrail.SummaryLimit);
        Assert.Equal(new string('a', 127), Encoding.UTF8.GetString(summary));

        trail.Navigated(new string('ж', 100));

        summary = trail.Summary();
        Assert.Equal(ActionTrail.SummaryLimit, summary.Length);
        Assert.Equal(new string('ж', 64), new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(summary));
    }

    /// <summary>
    /// След выписывается в отчёт о сбое после самого сбоя.
    /// </summary>
    [Fact]
    public async Task След_попадает_в_отчёт()
    {
        CrashReports reports = new(_folder, new TestTime(), DeviceInfo.Unknown);
        reports.Trail.Navigated("//report");
        reports.Trail.Action("ReportPage.xaml.cs", "OnGroupTapped");

        reports.Write(CrashKind.Caught, new TimeoutException());

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Equal("System.TimeoutException", report.Headline);
        Assert.EndsWith("--- trail (UTC)\n12:00:00.001 nav //report\n12:00:00.002 run ReportPage.OnGroupTapped", report.Text, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
