using System.Text;
using Finance.Application.Infrastructure.Diagnostics;

namespace Finance.Application.Tests;

/// <summary>
/// Причины прошлых завершений: какие записи системы дают отчёт, метка разобранного, разбор трасс
/// нативного падения и зависания — и что из трасс в отчёт не попадает.
/// </summary>
public sealed class PastExitsTests : IDisposable
{
    private static readonly DateTimeOffset Moment = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "finance-exits-" + Guid.NewGuid().ToString("N"));
    private readonly TestDevice _device = new();
    private readonly CrashReports _reports;

    public PastExitsTests() => _reports = new CrashReports(_folder, new TestTime(), DeviceInfo.Unknown);

    /// <summary>
    /// Обычные завершения отчёта не дают — закрыл пользователь, обновили, перезапуск после восстановления, —
    /// а выход с кодом не 0, убийство за расход ресурсов и нехватка памяти дают.
    /// </summary>
    [Fact]
    public async Task Обычные_завершения_отчёта_не_дают()
    {
        FakeHistory history = new(
            Exit(ExitReason.ExitSelf, minute: 1, status: 0),
            Exit(ExitReason.UserRequested, minute: 2),
            Exit(ExitReason.UserStopped, minute: 3),
            Exit(ExitReason.PackageUpdated, minute: 4),
            Exit(ExitReason.PackageStateChange, minute: 5),
            Exit(ExitReason.ExitSelf, minute: 6, status: 1),
            Exit(ExitReason.ExcessiveResourceUsage, minute: 7, description: "excessive binder traffic during cached"),
            Exit(ExitReason.LowMemory, minute: 8));

        Assert.Equal(3, await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None));

        IReadOnlyList<CrashReport> reports = await _reports.ReadAsync(CancellationToken.None);
        Assert.Equal(
            ["exit LowMemory status 0", "exit ExcessiveResourceUsage status 0", "exit ExitSelf status 1"],
            reports.Select(static report => report.Headline));
        Assert.All(reports, static report => Assert.Equal(CrashKind.ExitReason, report.Kind));
        Assert.Equal(Moment.AddMinutes(8), reports[0].AtUtc);
        Assert.Contains("description excessive binder traffic during cached", reports[1].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Разобранные записи второй раз не разбираются: метка — момент последней, в том числе пропущенной.
    /// </summary>
    [Fact]
    public async Task Записи_до_метки_второй_раз_не_разбираются()
    {
        FakeHistory history = new(Exit(ExitReason.LowMemory, minute: 1), Exit(ExitReason.UserRequested, minute: 2));
        PastExits exits = new(_reports, history, _device);

        Assert.Equal(1, await exits.RecordAsync(CancellationToken.None));
        Assert.Equal(Moment.AddMinutes(2).ToUnixTimeMilliseconds(), _device.Get(PastExits.MarkerName));

        history.Add(Exit(ExitReason.Signaled, minute: 3, status: 9));

        Assert.Equal(1, await exits.RecordAsync(CancellationToken.None));
        Assert.Equal(2, (await _reports.ReadAsync(CancellationToken.None)).Count);
        Assert.Equal(Moment.AddMinutes(2), history.LastAfter);
    }

    /// <summary>
    /// Падение, о котором уже написал обработчик, второй раз не пишется; без отчёта обработчика — пишется.
    /// </summary>
    [Fact]
    public async Task Падение_с_отчётом_обработчика_второй_раз_не_пишется()
    {
        // Отчёт обработчика записан моментом TestTime — середина дня; запись системы — через секунду после него
        _reports.WriteFatal(CrashKind.Unhandled, new TimeoutException());
        DateTimeOffset crashed = (await _reports.ReadAsync(CancellationToken.None))[0].AtUtc;

        FakeHistory history = new(
            new PastExit { AtUtc = crashed.AddSeconds(1), Reason = ExitReason.Crash, Status = 0, Description = "crash" },
            Exit(ExitReason.Crash, minute: 1, description: "java.lang.IllegalStateException: Пятёрочка"));

        Assert.Equal(1, await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None));

        CrashReport report = (await _reports.ReadAsync(CancellationToken.None)).Single(static report => report.Kind is CrashKind.ExitReason);
        Assert.Equal("exit Crash status 0", report.Headline);
        Assert.DoesNotContain("Пятёрочка", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Не записалось — метка встаёт перед этой записью, и при следующем запуске она разбирается снова:
    /// система отдаёт запись, пока та не вытеснена, а метка за ней потеряла бы её навсегда.
    /// </summary>
    [Fact]
    public async Task Незаписанная_запись_разбирается_при_следующем_запуске()
    {
        FakeHistory history = new(
            Exit(ExitReason.UserRequested, minute: 1),
            Exit(ExitReason.LowMemory, minute: 2),
            Exit(ExitReason.Signaled, minute: 3, status: 9));

        // Папку отчётов не создать: на её месте файл
        File.WriteAllText(_folder, "");
        PastExits exits = new(_reports, history, _device);

        Assert.Equal(0, await exits.RecordAsync(CancellationToken.None));
        Assert.Equal(Moment.AddMinutes(1).ToUnixTimeMilliseconds(), _device.Get(PastExits.MarkerName));

        File.Delete(_folder);

        Assert.Equal(2, await exits.RecordAsync(CancellationToken.None));
        Assert.Equal(Moment.AddMinutes(3).ToUnixTimeMilliseconds(), _device.Get(PastExits.MarkerName));
    }

    /// <summary>
    /// Нативное падение сразу за отчётом обработчика — та же смерть: среда .NET закрывает процесс
    /// прерыванием, и система записывает его нативным. Второго отчёта нет.
    /// </summary>
    [Fact]
    public async Task Нативное_падение_за_отчётом_обработчика_второй_раз_не_пишется()
    {
        _reports.WriteFatal(CrashKind.Unhandled, new TimeoutException());
        DateTimeOffset crashed = (await _reports.ReadAsync(CancellationToken.None))[0].AtUtc;

        FakeHistory history = new(new PastExit { AtUtc = crashed.AddSeconds(2), Reason = ExitReason.CrashNative, Status = 6, Trace = Tombstone() });

        Assert.Equal(0, await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None));
    }

    /// <summary>
    /// Отчёт о прошлом завершении встаёт среди отчётов по моменту завершения, а не по моменту записи.
    /// </summary>
    [Fact]
    public async Task Отчёт_о_прошлом_завершении_встаёт_по_своему_моменту()
    {
        // Отчёт обработчика — в полдень, прошлое завершение — в десять утра, но пишется позже
        _reports.Write(CrashKind.Warning, new TimeoutException());

        await new PastExits(_reports, new FakeHistory(Exit(ExitReason.LowMemory, minute: 0)), _device).RecordAsync(CancellationToken.None);

        Assert.Equal(
            [CrashKind.Warning, CrashKind.ExitReason],
            (await _reports.ReadAsync(CancellationToken.None)).Select(static report => report.Kind));
    }

    /// <summary>
    /// Порча посреди нативной трассы не стирает уже прочитанное: сигнал остаётся, а отчёт помечен как оборванный.
    /// </summary>
    [Fact]
    public async Task Порча_нативной_трассы_не_стирает_сигнал()
    {
        byte[] trace = [.. Tombstone()[..^4], 0xFF];

        FakeHistory history = new(new PastExit { AtUtc = Moment, Reason = ExitReason.CrashNative, Status = 11, Trace = trace });

        await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None);

        string text = Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text;
        Assert.Contains("signal 11 (SIGSEGV)", text, StringComparison.Ordinal);
        Assert.Contains("(__epoll_pwait+10)", text, StringComparison.Ordinal);
        Assert.Contains("native trace truncated", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Из трассы зависания берётся только своя секция: главные потоки других процессов в отчёт не попадают.
    /// </summary>
    [Fact]
    public async Task Из_трассы_зависания_берётся_только_свой_процесс()
    {
        const string trace = """
            ----- pid 17510 at 2026-10-06 20:00:42.781533100+0300 -----
            Cmd line: ru.finance.tracker

            "main" prio=5 tid=1 Sleeping
              at crc64.MainActivity.n_onCreate(Native method)

            ----- end 17510 -----

            ----- pid 1234 at 2026-10-06 20:00:43.000000000+0300 -----
            Cmd line: system_server

            "main" prio=5 tid=1 Native
              at com.android.server.SystemServer.run(SystemServer.java:1)

            ----- end 1234 -----
            """;

        FakeHistory history = new(new PastExit { AtUtc = Moment, Reason = ExitReason.Anr, Status = 0, Trace = Encoding.UTF8.GetBytes(trace) });

        await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None);

        string text = Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text;
        Assert.Contains("crc64.MainActivity.n_onCreate", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemServer", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Сводка, отданная системе заранее, попадает в отчёт: так видно, на каком экране случилось нативное падение.
    /// </summary>
    [Fact]
    public async Task Сводка_попадает_в_отчёт()
    {
        FakeHistory history = new(new PastExit
        {
            AtUtc = Moment,
            Reason = ExitReason.Signaled,
            Status = 9,
            Summary = Encoding.UTF8.GetBytes("//balances/transaction | TransactionPage.OnSaveClicked"),
        });

        await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None);

        Assert.Contains(
            "screen //balances/transaction | TransactionPage.OnSaveClicked",
            Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Из трассы нативного падения в отчёт идут сигнал и стек упавшего потока — и ничего больше: ни регистров,
    /// ни памяти, ни открытых файлов, ни сообщения о прерывании, ни стеков других потоков.
    /// </summary>
    [Fact]
    public async Task Из_нативной_трассы_берутся_сигнал_и_стек_упавшего_потока()
    {
        FakeHistory history = new(new PastExit { AtUtc = Moment, Reason = ExitReason.CrashNative, Status = 11, Trace = Tombstone() });

        await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None);

        string text = Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text;
        Assert.Contains("signal 11 (SIGSEGV), code 0 (SI_USER)", text, StringComparison.Ordinal);
        Assert.Contains("thread finance.tracker", text, StringComparison.Ordinal);
        Assert.Contains(
            "#00 pc 00000000000ddbea  /apex/com.android.runtime/lib64/bionic/libc.so (__epoll_pwait+10)",
            text,
            StringComparison.Ordinal);
        Assert.Contains("#01 pc 000000000001707d  /system/lib64/libutils.so (android::Looper::pollOnce(int, int*, int*, void**)+413)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Пятёрочка", text, StringComparison.Ordinal);
        Assert.DoesNotContain("finance.db", text, StringComparison.Ordinal);
        Assert.DoesNotContain("abort", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderThread", text, StringComparison.Ordinal);
        Assert.DoesNotContain("libhwui", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Испорченная трасса не роняет разбор: отчёт пишется с пометкой, что трассу не прочитать.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x52, 0xFF, 0x01 })]
    [InlineData(new byte[] { 0x80, 0x80 })]
    [InlineData(new byte[] { 0x07 })]
    public async Task Испорченная_нативная_трасса_не_роняет_разбор(byte[] trace)
    {
        FakeHistory history = new(new PastExit { AtUtc = Moment, Reason = ExitReason.CrashNative, Status = 11, Trace = trace });

        Assert.Equal(1, await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None));
        Assert.Contains("native trace unreadable", Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Из трассы зависания идут тема и стек главного потока — нативный и Java, — а другие потоки нет.
    /// </summary>
    [Fact]
    public async Task Из_трассы_зависания_берётся_главный_поток()
    {
        const string trace = """
            Subject: Input dispatching timed out (7c3177 ru.finance.tracker/ru.finance.tracker.MainActivity is not responding. Waited 5001ms for MotionEvent).
            RssKb: 334000

            ----- pid 17510 at 2026-10-06 20:00:42.781533100+0300 -----
            Cmd line: ru.finance.tracker

            "finance.tracker" sysTid=17510
                #00 pc 000000000008f808  /apex/com.android.runtime/lib64/bionic/libc.so (syscall+24)

            "Signal Catcher" sysTid=17511
                #00 pc 00000000000dd5ca  /apex/com.android.runtime/lib64/bionic/libc.so (__rt_sigtimedwait+10)

            "main" prio=5 tid=1 Sleeping
              at java.lang.Thread.sleep(Native method)
              at crc64.MainActivity.n_onCreate(Native method)

            "RenderThread" prio=7 tid=12 Native
              at libhwui.so
            """;

        FakeHistory history = new(new PastExit { AtUtc = Moment, Reason = ExitReason.Anr, Status = 0, Trace = Encoding.UTF8.GetBytes(trace) });

        await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None);

        string text = Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text;
        Assert.Contains("Subject: Input dispatching timed out", text, StringComparison.Ordinal);
        Assert.Contains("(syscall+24)", text, StringComparison.Ordinal);
        Assert.Contains("at java.lang.Thread.sleep(Native method)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Signal Catcher", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderThread", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RssKb", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Трасса зависания режется по пределу: целиком она — десятки килобайт.
    /// </summary>
    [Fact]
    public async Task Трасса_зависания_режется_по_пределу()
    {
        string frames = string.Join('\n', Enumerable.Range(0, 2000).Select(static index => $"  at a.b.C.method{index}(C.java:{index})"));
        string trace = $"\"main\" prio=5 tid=1 Native\n{frames}\n";

        FakeHistory history = new(new PastExit { AtUtc = Moment, Reason = ExitReason.Anr, Status = 0, Trace = Encoding.UTF8.GetBytes(trace) });

        await new PastExits(_reports, history, _device).RecordAsync(CancellationToken.None);

        string text = Assert.Single(await _reports.ReadAsync(CancellationToken.None)).Text;
        Assert.True(text.Length < AnrTrace.Limit + 1024, $"длина {text.Length}");
        Assert.Contains("method0(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("method1999(", text, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static PastExit Exit(ExitReason reason, int minute, int status = 0, string? description = null) =>
        new() { AtUtc = Moment.AddMinutes(minute), Reason = reason, Status = status, Description = description };

    /// <summary>
    /// Образец трассы нативного падения: упавший поток, соседний поток и всё, чего в отчёте быть не должно.
    /// </summary>
    private static byte[] Tombstone()
    {
        byte[] frame(ulong pc, string file, string function, ulong offset) => Proto.Message(
            4,
            Proto.Varint(1, pc),
            Proto.Varint(2, pc + 0x7ac6a8682000),
            Proto.String(4, function),
            Proto.Varint(5, offset),
            Proto.String(6, file),
            Proto.String(8, "fcb82240218d1473de1e3d2137c0be35"));

        byte[] crashed = Proto.Join(
            Proto.Varint(1, 16077),
            Proto.String(2, "finance.tracker"),
            Proto.Message(3, Proto.String(1, "rax"), Proto.Varint(2, 0xfffffffffffffffc)),
            frame(0xddbea, "/apex/com.android.runtime/lib64/bionic/libc.so", "__epoll_pwait", 10),
            frame(0x1707d, "/system/lib64/libutils.so", "android::Looper::pollOnce(int, int*, int*, void**)", 413),
            Proto.Message(5, Proto.String(1, "stack"), Proto.String(3, "Пятёрочка 1 234,56")));

        byte[] other = Proto.Join(
            Proto.Varint(1, 16090),
            Proto.String(2, "RenderThread"),
            frame(0x1234, "/system/lib64/libhwui.so", "android::uirenderer::renderthread::RenderThread::threadLoop()", 1));

        return Proto.Join(
            Proto.Varint(1, 2),
            Proto.String(2, "google/sdk_gphone64_x86_64"),
            Proto.Varint(5, 16077),
            Proto.Varint(6, 16077),
            Proto.Message(
                10,
                Proto.Varint(1, 11),
                Proto.String(2, "SIGSEGV"),
                Proto.Varint(3, 0),
                Proto.String(4, "SI_USER"),
                Proto.Message(10, Proto.String(1, "Пятёрочка"))),
            Proto.String(14, "abort: Пятёрочка"),
            Proto.Message(16, Proto.Varint(1, 16090), Proto.Message(2, other)),
            Proto.Message(16, Proto.Varint(1, 16077), Proto.Message(2, crashed)),
            Proto.Message(17, Proto.String(1, "/data/data/ru.finance.tracker/files/finance.db")),
            Proto.Message(18, Proto.String(1, "main"), Proto.Message(2, Proto.String(7, "Пятёрочка"))),
            Proto.Message(19, Proto.Varint(1, 42), Proto.String(2, "/data/user/0/ru.finance.tracker/files/finance.db")),
            Proto.Fixed64(23, 1));
    }

    /// <summary>
    /// Записи системы в памяти.
    /// </summary>
    private sealed class FakeHistory(params PastExit[] exits) : IExitHistory
    {
        private readonly List<PastExit> _exits = [.. exits];

        public DateTimeOffset LastAfter { get; private set; }

        public void Add(PastExit exit) => _exits.Add(exit);

        public IReadOnlyList<PastExit> ReadAfter(DateTimeOffset afterUtc)
        {
            LastAfter = afterUtc;

            return [.. _exits.Where(exit => exit.AtUtc > afterUtc)];
        }
    }
}
