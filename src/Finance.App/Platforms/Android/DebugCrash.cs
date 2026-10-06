#if DEBUG
using System.Collections.Frozen;
using Android.Content;
using Android.OS;

namespace Finance.App;

/// <summary>
/// Нарочный сбой для проверки отчётов о сбоях на эмуляторе — только в отладочной сборке. Вызывается намерением:
/// <c>am start -n ru.finance.tracker/ru.finance.tracker.MainActivity --es debug_crash вид --es debug_crash_id номер</c>.
/// </summary>
/// <remarks>
/// Номер у каждого вызова свой: упавшее приложение система поднимает с тем же намерением, и без номера
/// сбой повторялся бы на каждом перезапуске. Вызов с уже виденным номером или без номера не делает ничего.
/// Виды: <c>caught</c> — сбой действия через <see cref="Guarded"/>, <c>ui</c> — исключение .NET на главном потоке,
/// <c>thread</c> и <c>timer</c> — вне главного потока, <c>task</c> — забытая задача, <c>java</c> — исключение Java
/// на главном потоке, <c>anr</c> — главный поток спит минуту: касание по окну через пять секунд даёт окно
/// «не отвечает». Сбой приходит через две секунды: к этому времени окно уже на экране.
/// </remarks>
internal static class DebugCrash
{
    private const string Extra = "debug_crash";
    private const string IdExtra = "debug_crash_id";
    private const int DelayMilliseconds = 2000;

    private static readonly FrozenSet<string> Kinds = FrozenSet.Create("caught", "ui", "thread", "timer", "task", "java", "anr");

    /// <summary>
    /// Держит таймер до срабатывания: без ссылки сборщик мусора убирает его раньше.
    /// </summary>
    private static Timer? _timer;

    /// <summary>
    /// Вызывает сбой, если намерение его просит.
    /// </summary>
    /// <param name="intent">Намерение запуска.</param>
    internal static void Accept(Intent? intent)
    {
        string? kind = intent?.GetStringExtra(Extra);
        string? id = intent?.GetStringExtra(IdExtra);

        // Неизвестный вид номер не тратит: опечатку можно повторить с тем же номером
        if (kind is null || !Kinds.Contains(kind) || string.IsNullOrEmpty(id) || Preferences.Default.Get(IdExtra, "") == id)
        {
            return;
        }

        Preferences.Default.Set(IdExtra, id);

        Handler main = new(Looper.MainLooper!);

        switch (kind)
        {
            case "caught":
                main.PostDelayed(static () => Guarded.Run(static () => throw new InvalidOperationException("debug caught")), DelayMilliseconds);
                break;
            case "ui":
                main.PostDelayed(static () => throw new InvalidOperationException("debug ui"), DelayMilliseconds);
                break;
            case "thread":
                new Thread(static () =>
                {
                    Thread.Sleep(DelayMilliseconds);
                    throw new InvalidOperationException("debug thread");
                }).Start();
                break;
            case "timer":
                _timer = new Timer(static _ => throw new InvalidOperationException("debug timer"), null, DelayMilliseconds, Timeout.Infinite);
                break;
            case "task":
                _ = Task.Run(static () => throw new InvalidOperationException("debug task"));
                new Thread(static () =>
                {
                    Thread.Sleep(DelayMilliseconds);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }).Start();
                break;
            case "java":
                main.PostDelayed(static () => throw new Java.Lang.IllegalStateException("debug java"), DelayMilliseconds);
                break;
            case "anr":
                main.PostDelayed(static () => Thread.Sleep(TimeSpan.FromMinutes(1)), DelayMilliseconds);
                break;
        }
    }
}
#endif
