using Finance.Application.Infrastructure.AppLock;
using AndroidSettings = Android.Provider.Settings;

namespace Finance.App;

/// <summary>
/// Часы с включения телефона, идущие и во сне.
/// </summary>
/// <remarks>
/// <c>Stopwatch</c> и <c>Environment.TickCount64</c> на Android берут <c>CLOCK_MONOTONIC</c>,
/// а он стоит, пока телефон спит: час в кармане приложение приняло бы за секунды.
/// <c>ElapsedRealtime</c> — это <c>CLOCK_BOOTTIME</c>.
/// </remarks>
internal sealed class DeviceUptime : IUptime
{
    /// <inheritdoc />
    public long SinceBoot => Android.OS.SystemClock.ElapsedRealtime();

    /// <inheritdoc />
    public long BootCount =>
        AndroidSettings.Global.GetInt(Platform.AppContext.ContentResolver, AndroidSettings.Global.BootCount, 0);
}
