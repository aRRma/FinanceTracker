using System.Globalization;
using System.Text;
using Finance.Application.Infrastructure.AppLock;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Причины прошлых завершений: при запуске новые записи системы превращаются в отчёты о сбоях. Так
/// становится видно то, что не ловит ни один обработчик в процессе, — нативное падение, зависание,
/// убийство в фоне.
/// </summary>
/// <remarks>
/// Разобранные записи помнятся моментом последней в настройках устройства: иначе каждая давала бы отчёт на
/// каждом запуске. Обычные завершения отчёта не дают, а падение, о котором уже написал обработчик, второй раз
/// не пишется.
/// </remarks>
public sealed class PastExits
{
    /// <summary>
    /// Имя метки в настройках устройства: момент последней разобранной записи в миллисекундах Unix.
    /// </summary>
    internal const string MarkerName = "crash_reports.last_exit_ms";

    /// <summary>
    /// Окно, в котором отчёт обработчика считается отчётом о том же падении: обработчик пишет за миг до смерти
    /// процесса, а система отмечает её чуть позже.
    /// </summary>
    private static readonly TimeSpan HandlerWindow = TimeSpan.FromSeconds(10);

    private readonly CrashReports _reports;
    private readonly IExitHistory _history;
    private readonly IDevicePreferences _preferences;

    /// <summary>
    /// Разбор записей о прошлых завершениях.
    /// </summary>
    /// <param name="reports">Отчёты о сбоях.</param>
    /// <param name="history">Записи системы.</param>
    /// <param name="preferences">Настройки устройства — для метки разобранного.</param>
    public PastExits(CrashReports reports, IExitHistory history, IDevicePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(preferences);

        _reports = reports;
        _history = history;
        _preferences = preferences;
    }

    /// <summary>
    /// Пишет отчёты о новых записях системы и сдвигает метку.
    /// </summary>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Сколько отчётов записано.</returns>
    public async Task<int> RecordAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset after = _preferences.Get(MarkerName) is { } marker
            ? DateTimeOffset.FromUnixTimeMilliseconds(marker)
            : DateTimeOffset.MinValue;

        // История и трассы читаются у системы синхронно: на главный поток это не пускается
        IReadOnlyList<PastExit> exits = await Task.Run(() => _history.ReadAfter(after), cancellationToken).ConfigureAwait(false);

        if (exits.Count is 0)
        {
            return 0;
        }

        IReadOnlyList<CrashReport> known = await _reports.ReadAsync(cancellationToken).ConfigureAwait(false);
        int written = 0;
        DateTimeOffset? done = null;

        foreach (PastExit exit in exits.OrderBy(static exit => exit.AtUtc))
        {
            if (!IsOrdinary(exit) && !IsKnown(exit, known))
            {
                // Не записалось — метка встаёт перед этой записью: иначе система больше её не отдаст,
                // и о нативном падении или зависании не узнать никогда. Повтор — при следующем запуске
                if (!_reports.WriteExit(exit.AtUtc, Describe(exit)))
                {
                    break;
                }

                written++;
            }

            // Метка двигается и за пропущенными: обычное завершение не станет интереснее при следующем запуске
            done = exit.AtUtc;
        }

        if (done is { } last)
        {
            _preferences.Set(MarkerName, last.ToUnixTimeMilliseconds());
        }

        return written;
    }

    /// <summary>
    /// Обычное завершение: пользователь закрыл приложение, его обновили, оно перезапустилось само после
    /// восстановления из выгрузки.
    /// </summary>
    internal static bool IsOrdinary(PastExit exit) => exit.Reason switch
    {
        ExitReason.ExitSelf => exit.Status is 0,
        ExitReason.UserRequested or ExitReason.UserStopped or ExitReason.PackageUpdated or ExitReason.PackageStateChange => true,
        _ => false,
    };

    /// <summary>
    /// О падении уже написал обработчик в процессе — у него стек полнее, чем у системы. Нативное падение
    /// тоже: среда .NET после необработанного исключения может закрыть процесс прерыванием, и система
    /// запишет его нативным — а это та же смерть, что уже в отчёте.
    /// </summary>
    /// <remarks>
    /// Окно несимметрично: обработчик пишет до смерти процесса, а система отмечает её после. Секунда
    /// в другую сторону — запас на округление момента системой.
    /// </remarks>
    private static bool IsKnown(PastExit exit, IReadOnlyList<CrashReport> known) =>
        exit.Reason is ExitReason.Crash or ExitReason.ExitSelf or ExitReason.CrashNative
        && known.Any(report =>
            report.Kind is CrashKind.Unhandled or CrashKind.Java
            && report.AtUtc >= exit.AtUtc - HandlerWindow
            && report.AtUtc <= exit.AtUtc + TimeSpan.FromSeconds(1));

    private static string Describe(PastExit exit)
    {
        StringBuilder text = new();
        text.Append(CultureInfo.InvariantCulture, $"exit {exit.Reason} status {exit.Status}\n");

        // Описание пишет система, но у необработанного исключения Java в нём может оказаться
        // текст исключения — его не пишем, как и сам текст в отчётах обработчиков
        if (exit.Reason is not ExitReason.Crash && !string.IsNullOrWhiteSpace(exit.Description))
        {
            text.Append("description ").Append(exit.Description).Append('\n');
        }

        if (exit.Summary is { Length: > 0 } summary)
        {
            text.Append("screen ").Append(Encoding.UTF8.GetString(summary)).Append('\n');
        }

        if (exit.Trace is { Length: > 0 } trace)
        {
            text.Append(exit.Reason switch
            {
                ExitReason.CrashNative => NativeTrace.Describe(trace),
                ExitReason.Anr => AnrTrace.Describe(Encoding.UTF8.GetString(trace)),
                _ => "",
            });
        }

        return text.ToString();
    }
}
