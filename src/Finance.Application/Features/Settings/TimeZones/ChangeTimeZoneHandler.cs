using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Features.Settings.TimeZones;

/// <summary>
/// Меняет часовой пояс приложения. Пояс хранится идентификатором зоны, а не
/// числовым смещением: смещение перестаёт соответствовать зоне на переходе
/// на летнее время, и «сегодня» уезжает на сутки.
/// </summary>
public sealed class ChangeTimeZoneHandler : IChangeTimeZoneHandler
{
    private readonly ILocalSettings _settings;
    private readonly SystemClock _clock;
    private readonly IChangeNotifier _changes;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="settings">Локальные настройки устройства.</param>
    /// <param name="clock">Часы приложения: им ставится пояс.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ChangeTimeZoneHandler(ILocalSettings settings, SystemClock clock, IChangeNotifier changes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(changes);

        _settings = settings;
        _clock = clock;
        _changes = changes;
    }

    /// <inheritdoc />
    public async Task HandleAsync(string? id, CancellationToken cancellationToken = default)
    {
        if (id is null)
        {
            await _settings
                .RemoveAsync(SettingName.TimeZoneId, cancellationToken)
                .ConfigureAwait(false);

            _clock.TimeZone = TimeZoneInfo.Local;
        }
        else
        {
            // Проверка до записи: незнакомую зону нельзя ни поставить часам,
            // ни сохранить — иначе следующий запуск молча откатится на системную,
            // а пользователь будет уверен, что выбор сохранён
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone))
            {
                throw new TimeZoneNotFoundException(Faults.TimeZoneUnknown(id));
            }

            await _settings
                .SetAsync(SettingName.TimeZoneId, id, cancellationToken)
                .ConfigureAwait(false);

            _clock.TimeZone = zone;
        }

        // Вместе с поясом меняется «сегодня»: форма операции подставляет дату,
        // а лента и балансы считают итог дня — перечитаться обязаны и они
        _changes.Publish(DataChange.Settings | DataChange.Transactions);
    }
}
