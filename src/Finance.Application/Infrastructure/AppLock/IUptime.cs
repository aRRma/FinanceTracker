namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Время с включения телефона — для отсчёта фона и паузы после неверных попыток.
/// </summary>
/// <remarks>
/// Не <see cref="IClock"/>: это длительность, а не момент. Часы приложения обходятся
/// переводом времени назад, а <c>Stopwatch</c> на Android стоит, пока телефон спит, —
/// на телефоне этим часам отвечает <c>SystemClock.ElapsedRealtime</c>.
/// </remarks>
public interface IUptime
{
    /// <summary>
    /// Миллисекунды с включения телефона, включая время сна.
    /// </summary>
    long SinceBoot { get; }

    /// <summary>
    /// Номер включения телефона: после перезагрузки отсчёт <see cref="SinceBoot"/> начинается заново.
    /// </summary>
    long BootCount { get; }
}
