namespace Finance.Application.Infrastructure;

/// <summary>
/// Возврат приложения из фона. Пока оно лежало свёрнутым, телефон мог сменить
/// часовой пояс — перелёт, поездка, — а «сегодня» приложения осталось бы прежним
/// до перезапуска, и операция «сегодня» легла бы на дату места вылета.
/// </summary>
public sealed class TimeZoneFollower
{
    private readonly SystemClock _clock;
    private readonly IChangeNotifier _changes;

    /// <summary>
    /// Создаёт наблюдателя.
    /// </summary>
    /// <param name="clock">Часы приложения.</param>
    /// <param name="changes">Оповещение экранов о перемене.</param>
    public TimeZoneFollower(SystemClock clock, IChangeNotifier changes)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(changes);

        _clock = clock;
        _changes = changes;
    }

    /// <summary>
    /// Перечитывает системный пояс. Сменился — экраны перечитываются, как при выборе
    /// пояса в настройках: форма подставляет дату, лента и балансы считают итог дня.
    /// Выбранный пользователем пояс от телефона не зависит, и тогда делать нечего.
    /// </summary>
    public void Resume()
    {
        if (_clock.RefreshSystemZone())
        {
            _changes.Publish(DataChange.Settings | DataChange.Transactions);
        }
    }
}
