namespace Finance.Application.Infrastructure;

/// <summary>
/// Оповещение экранов об изменении данных. Событие приходит после фиксации
/// транзакции, а не до: экран, перечитавший себя внутри неоткатанной транзакции,
/// показал бы то, чего в базе может и не остаться.
/// </summary>
public interface IChangeNotifier
{
    /// <summary>Данные изменились. Доставка в поток интерфейса — забота подписчика.</summary>
    event Action<DataChange>? Changed;

    /// <summary>Сообщает об изменении. Вызывается единой точкой выполнения команд.</summary>
    /// <param name="change">Что именно изменилось.</param>
    void Publish(DataChange change);
}
