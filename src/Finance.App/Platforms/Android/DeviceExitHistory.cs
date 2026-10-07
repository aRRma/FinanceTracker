using Android.App;
using Android.Content;
using Finance.Application.Infrastructure.Diagnostics;

namespace Finance.App;

/// <summary>
/// Записи Android о прошлых завершениях процесса — <c>ActivityManager.GetHistoricalProcessExitReasons</c>.
/// Система держит последние шестнадцать.
/// </summary>
internal sealed class DeviceExitHistory : IExitHistory
{
    /// <inheritdoc />
    public IReadOnlyList<PastExit> ReadAfter(DateTimeOffset afterUtc)
    {
        if (Android.App.Application.Context.GetSystemService(Context.ActivityService) is not ActivityManager activities)
        {
            return [];
        }

        List<PastExit> exits = [];

        foreach (ApplicationExitInfo info in activities.GetHistoricalProcessExitReasons(packageName: null, pid: 0, maxNum: 0))
        {
            DateTimeOffset atUtc = DateTimeOffset.FromUnixTimeMilliseconds(info.Timestamp);

            if (atUtc <= afterUtc)
            {
                continue;
            }

            ExitReason reason = (ExitReason)(int)info.Reason;
            reason = Enum.IsDefined(reason) ? reason : ExitReason.Unknown;

            exits.Add(new PastExit
            {
                AtUtc = atUtc,
                Reason = reason,
                Status = info.Status,
                Description = info.Description,
                Summary = info.GetProcessStateSummary(),

                // Трасса есть только у нативного падения и зависания и весит десятки и сотни килобайт: у остальных не читается
                Trace = reason is ExitReason.CrashNative or ExitReason.Anr ? ReadTrace(info) : null,
            });
        }

        return exits;
    }

    private static byte[]? ReadTrace(ApplicationExitInfo info)
    {
        try
        {
            using Stream? trace = info.TraceInputStream;

            if (trace is null)
            {
                return null;
            }

            using MemoryStream buffer = new();
            trace.CopyTo(buffer);

            return buffer.ToArray();
        }
        catch (Exception failure) when (failure is IOException or Java.IO.IOException)
        {
            // Без трассы отчёт всё равно скажет причину и экран
            Android.Util.Log.Warn("Finance", failure.ToString());

            return null;
        }
    }
}
