namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Пауза перед следующей попыткой после неверных: без неё четыре цифры
/// подбираются перебором за вечер.
/// </summary>
public static class EntryPause
{
    /// <summary>
    /// Неверных попыток без паузы.
    /// </summary>
    public const int FreeAttempts = 5;

    private static readonly TimeSpan[] Steps =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];

    /// <summary>
    /// Пауза после очередной неверной попытки: до пятой — нет, дальше растёт до часа и держится на нём.
    /// </summary>
    /// <param name="failures">Неверных попыток подряд, включая эту.</param>
    public static TimeSpan After(int failures) =>
        failures < FreeAttempts ? TimeSpan.Zero : Steps[Math.Min(failures - FreeAttempts, Steps.Length - 1)];
}
