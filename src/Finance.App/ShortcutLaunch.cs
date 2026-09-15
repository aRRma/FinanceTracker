using Finance.Domain;

namespace Finance.App;

/// <summary>
/// Запрос на запись операции, пришедший с ярлыка на значке приложения.
/// </summary>
/// <remarks>
/// Намерение холодного старта приходит раньше, чем существует каркас навигации:
/// платформа создаёт активность, и только потом появляется первая страница.
/// Поэтому вид запоминается здесь и забирается каркасом, когда тот готов.
/// </remarks>
internal static class ShortcutLaunch
{
    private static readonly Lock Gate = new();

    private static TransactionKind? _pending;

    /// <summary>Открыть форму операции этого вида — сейчас или как только станет чем.</summary>
    /// <param name="kind">Вид операции с ярлыка.</param>
    public static void Request(TransactionKind kind)
    {
        lock (Gate)
        {
            _pending = kind;
        }

        // Каркас живёт в потоке интерфейса, а намерение приходит в поток платформы
        MainThread.BeginInvokeOnMainThread(Deliver);
    }

    /// <summary>
    /// Отдаёт отложенный запрос каркасу. Зовётся после навигации: до первой
    /// страницы переходить некуда, и запрос ждёт своего часа.
    /// </summary>
    public static void Deliver()
    {
        if (Shell.Current is not AppShell shell)
        {
            return;
        }

        TransactionKind? kind;

        lock (Gate)
        {
            kind = _pending;
            _pending = null;
        }

        if (kind is { } wanted)
        {
            Guarded.Run(() => shell.OpenTransactionAsync(wanted));
        }
    }

    /// <summary>Разбирает вид из дополнения к намерению.</summary>
    /// <param name="value">Значение дополнения <c>kind</c>.</param>
    /// <returns>Вид операции или <c>null</c>, если значение чужое.</returns>
    public static TransactionKind? Parse(string? value) =>
        Enum.TryParse(value, out TransactionKind kind) ? kind : null;
}
