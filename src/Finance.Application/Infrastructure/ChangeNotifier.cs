using System.Numerics;

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

    // Номер последнего изменения по каждому разряду DataChange
    private readonly long[] _versions = new long[sizeof(int) * 8];

    private long _last;

    /// <summary>
    /// Создаёт оповещение.
    /// </summary>
    /// <param name="dispatch">Как выполнить действие в потоке интерфейса. Пусто — выполняется на месте.</param>
    public ChangeNotifier(Action<Action>? dispatch = null) =>
        _dispatch = dispatch ?? (static action => action());

    /// <inheritdoc />
    public event Action<DataChange>? Changed;

    /// <inheritdoc />
    public long VersionOf(DataChange kinds)
    {
        long version = 0;

        for (uint bits = (uint)kinds; bits is not 0; bits &= bits - 1)
        {
            version = Math.Max(version, Interlocked.Read(ref _versions[BitOperations.TrailingZeroCount(bits)]));
        }

        return version;
    }

    /// <inheritdoc />
    public void Publish(DataChange change)
    {
        if (change is DataChange.None)
        {
            return;
        }

        // Номер растёт там же, где приходит событие, а не при публикации: иначе экран,
        // ушедший между ними, запомнил бы номер изменения, которого так и не получил
        _dispatch(() =>
        {
            long version = Interlocked.Increment(ref _last);

            for (uint bits = (uint)change; bits is not 0; bits &= bits - 1)
            {
                Interlocked.Exchange(ref _versions[BitOperations.TrailingZeroCount(bits)], version);
            }

            Changed?.Invoke(change);
        });
    }
}
