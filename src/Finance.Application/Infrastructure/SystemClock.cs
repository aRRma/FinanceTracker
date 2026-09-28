namespace Finance.Application.Infrastructure;

/// <summary>
/// Часы приложения. Зона хранится идентификатором и подставляется целиком, а не
/// числовым смещением: смещение перестаёт соответствовать зоне на переходе
/// на летнее время, и «сегодня» съезжает на сутки.
/// </summary>
/// <param name="time">Источник момента: в приложении системный, в тестах заданный, чтобы «сегодня» не зависело от дня запуска.</param>
public sealed class SystemClock(TimeProvider time) : IClock
{
    /// <summary>
    /// Зона пользователя. По умолчанию системная; настройка приложения
    /// переопределяет её при запуске и сразу при выборе другой зоны.
    /// </summary>
    public TimeZoneInfo TimeZone
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = TimeZoneInfo.Local;

    /// <inheritdoc />
    public DateTimeOffset NowUtc => time.GetUtcNow();

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(NowUtc, TimeZone).DateTime);
}
