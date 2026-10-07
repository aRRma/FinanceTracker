namespace Finance.Application.Infrastructure;

/// <summary>
/// Время для данных и экранов приложения; диагностика берёт <see cref="TimeProvider"/>,
/// потому что создаётся раньше контейнера. Отдельный тип, а не обращения к
/// <see cref="DateTimeOffset.UtcNow"/> по месту: «сегодня» пользователя зависит
/// от его часового пояса, и код, берущий дату из UTC, ошибается на сутки каждый вечер.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Текущий момент в UTC. Всё, что попадает в метки <c>_at_utc</c>.
    /// </summary>
    DateTimeOffset NowUtc { get; }

    /// <summary>
    /// Сегодняшняя календарная дата пользователя — в его зоне, а не в UTC.
    /// </summary>
    DateOnly Today { get; }

    /// <summary>
    /// Часовой пояс пользователя. Отображение моментов времени идёт через него.
    /// </summary>
    TimeZoneInfo TimeZone { get; }
}
