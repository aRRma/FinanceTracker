namespace Finance.Application.Infrastructure;

/// <summary>
/// Оповещение экранов об изменении данных внутри одного процесса.
/// </summary>
/// <remarks>
/// Команды выполняются в фоне, а подписчики правят привязанные коллекции —
/// это можно делать только из потока интерфейса. Как в него попасть, знает
/// платформа, поэтому способ доставки подаётся снаружи; в тестах его нет,
/// и событие приходит на месте.
/// </remarks>
public sealed class ChangeNotifier : IChangeNotifier
{
    private readonly Action<Action> _dispatch;

    /// <summary>
    /// Создаёт оповещение.
    /// </summary>
    /// <param name="dispatch">Как выполнить действие в потоке интерфейса. Пусто — выполняется на месте.</param>
    public ChangeNotifier(Action<Action>? dispatch = null) =>
        _dispatch = dispatch ?? (static action => action());

    /// <inheritdoc />
    public event Action<DataChange>? Changed;

    /// <inheritdoc />
    public void Publish(DataChange change)
    {
        if (change is DataChange.None)
        {
            return;
        }

        _dispatch(() => Changed?.Invoke(change));
    }
}
