namespace Finance.Application.Infrastructure;

/// <summary>
/// Часы приложения. Зона хранится идентификатором и подставляется целиком, а не
/// числовым смещением: смещение перестаёт соответствовать зоне на переходе
/// на летнее время, и «сегодня» съезжает на сутки.
/// </summary>
/// <param name="time">Источник момента: в приложении системный, в тестах заданный, чтобы «сегодня» не зависело от дня запуска.</param>
public sealed class SystemClock(TimeProvider time) : IClock
{
    private TimeZoneInfo _zone = TimeZoneInfo.Local;

    /// <summary>
    /// Зона пользователя. По умолчанию системная; настройка приложения
    /// переопределяет её при запуске и сразу при выборе другой зоны.
    /// </summary>
    public TimeZoneInfo TimeZone
    {
        get => _zone;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _zone = value;
            FollowsSystem = false;
        }
    }

    /// <summary>
    /// Зона — системная, своей пользователь не выбирал. Тогда она следует за телефоном:
    /// прилетевший в другой пояс видит своё «сегодня», а не дату места вылета.
    /// </summary>
    public bool FollowsSystem { get; private set; } = true;

    /// <inheritdoc />
    public DateTimeOffset NowUtc => time.GetUtcNow();

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(NowUtc, _zone).DateTime);

    /// <summary>
    /// Возвращает часы к системной зоне — выбор «Как в системе».
    /// </summary>
    public void FollowSystem()
    {
        FollowsSystem = true;
        RefreshSystemZone();
    }

    /// <summary>
    /// Перечитывает системную зону, если часы за ней следуют. Зону система отдаёт
    /// из кэша, снятого при первом обращении: без сброса сменённый в телефоне пояс
    /// приложение увидело бы только после перезапуска.
    /// </summary>
    /// <returns><c>true</c>, если зона сменилась и «сегодня» могло стать другим.</returns>
    public bool RefreshSystemZone()
    {
        if (!FollowsSystem)
        {
            return false;
        }

        TimeZoneInfo.ClearCachedData();
        TimeZoneInfo local = TimeZoneInfo.Local;

        if (string.Equals(local.Id, _zone.Id, StringComparison.Ordinal))
        {
            return false;
        }

        _zone = local;

        return true;
    }
}
