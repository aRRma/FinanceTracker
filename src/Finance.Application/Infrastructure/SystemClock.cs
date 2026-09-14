namespace Finance.Application.Infrastructure;

/// <summary>
/// Часы приложения. Зона хранится идентификатором и подставляется целиком, а не
/// числовым смещением: смещение перестаёт соответствовать зоне на переходе
/// на летнее время, и «сегодня» съезжает на сутки.
/// </summary>
public sealed class SystemClock : IClock
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
    public DateTimeOffset NowUtc => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(NowUtc, TimeZone).DateTime);
}
