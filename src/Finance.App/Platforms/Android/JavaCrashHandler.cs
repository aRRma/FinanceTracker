namespace Finance.App;

/// <summary>
/// Обработчик Java для исключений, которые никто не поймал: пишет отчёт и отдаёт исключение прежнему обработчику.
/// </summary>
/// <remarks>
/// Прежний — обработчик среды .NET: он передаёт падение в <see cref="AppDomain.UnhandledException"/>
/// и закрывает процесс. Без передачи процесс повис бы с мёртвым потоком.
/// </remarks>
/// <param name="previous">Обработчик, стоявший до этого.</param>
internal sealed class JavaCrashHandler(Java.Lang.Thread.IUncaughtExceptionHandler? previous)
    : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
{
    /// <inheritdoc />
    public void UncaughtException(Java.Lang.Thread t, Java.Lang.Throwable e)
    {
        // Обёртка .NET знает только управляемую часть стека, а у падения внутри Java она пуста.
        // Исключение .NET, ушедшее в Java, к этому моменту уже записано, и вызов промолчит
        // Стек Android отдаёт пустым, если в цепочке есть UnknownHostException, — тогда остаётся хотя бы класс
        string? stack = Android.Util.Log.GetStackTraceString(e);
        CrashCatcher.Reports?.WriteFatalJava(string.IsNullOrEmpty(stack) ? e.Class?.Name ?? nameof(Java.Lang.Throwable) : stack);

        previous?.UncaughtException(t, e);
    }
}
