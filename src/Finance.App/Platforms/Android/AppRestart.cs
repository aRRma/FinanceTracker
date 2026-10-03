using Android.Content;

namespace Finance.App;

/// <summary>
/// Перезапуск приложения с чистого листа. <c>Application.Quit()</c> им не является:
/// на Android процесс и синглтоны контейнера живут дальше, и приложение вернулось
/// бы с прежними экранами и запомненными при запуске настройками.
/// </summary>
internal static class AppRestart
{
    /// <summary>
    /// Запускает приложение заново новой задачей и завершает текущий процесс.
    /// </summary>
    internal static void Run()
    {
        Context context = Platform.AppContext;
        Intent? launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);

        if (launch?.Component is { } component)
        {
            // Новая задача с очисткой прежней: «назад» не вернёт на экраны до восстановления
            context.StartActivity(Intent.MakeRestartActivityTask(component));
        }

        // Запуск уже передан системе: она поднимет приложение новым процессом
        Java.Lang.JavaSystem.Exit(0);
    }
}
