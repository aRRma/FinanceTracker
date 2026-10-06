using System.Runtime.CompilerServices;
using Finance.Application.Texts;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Errors;

namespace Finance.App;

/// <summary>
/// Обёртка обработчиков событий. Обработчик события обязан быть <c>async void</c>,
/// а невыловленное исключение в <c>async void</c> не всплывает к вызывающему —
/// оно роняет процесс. Окно при этом просто закрывается: ни сборка, ни экран
/// ничего не скажут, и причина останется только в журнале устройства.
/// </summary>
/// <remarks>
/// Это единственное место в <c>Finance.App</c>, где <c>async void</c> разрешён,
/// и единственное, где он с перехватом. Разметка зовёт синхронный обработчик,
/// тело уезжает в <c>async Task</c> — так компилятор сам не даёт забыть перехват.
/// </remarks>
internal static class Guarded
{
    /// <summary>
    /// Тег журнала устройства, под которым пишутся перехваченные сбои.
    /// </summary>
    private const string LogTag = "Finance";

    /// <summary>
    /// Выполняет тело обработчика, показывая сбой вместо молчаливого закрытия окна.
    /// </summary>
    /// <param name="action">Тело обработчика.</param>
    /// <param name="file">Файл обработчика — для следа действий; подставляет компилятор.</param>
    /// <param name="member">Имя обработчика — для следа действий; подставляет компилятор.</param>
    internal static void Run(
        Func<Task> action,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "")
    {
        ArgumentNullException.ThrowIfNull(action);

        // Обработчики событий собраны здесь все: одна строка вместо разметки каждой кнопки
        CrashCatcher.Trail?.Action(file, member);

        RunCore(action);
    }

    private static async void RunCore(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await ReportAsync(UiTexts.ErrorActionFailedTitle, error);
        }
    }

    /// <summary>
    /// Выполняет команду модели, если она сейчас доступна. Команду, привязанную
    /// в разметке, запускает <see cref="System.Windows.Input.ICommand.Execute"/>, и её сбой
    /// уходит в поток интерфейса мимо всякого перехвата — окно закрывается молча.
    /// Поэтому асинхронные команды зовутся из обработчика, а не привязкой.
    /// </summary>
    /// <param name="command">Команда модели.</param>
    /// <param name="file">Файл обработчика — для следа действий; подставляет компилятор.</param>
    /// <param name="member">Имя обработчика — для следа действий; подставляет компилятор.</param>
    internal static void Execute(
        IAsyncRelayCommand command,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "")
    {
        ArgumentNullException.ThrowIfNull(command);

        // Недоступная команда не выполняется: так ведёт себя и привязка, и на этом
        // держится защита от второго нажатия, пока первое ещё не отработало
        if (command.CanExecute(null))
        {
            Run(() => command.ExecuteAsync(null), file, member);
        }
    }

    /// <summary>
    /// Сообщает о сбое, пойманном в другом месте: он пишется в журнал и показывается
    /// так же, как сбой обработчика.
    /// </summary>
    /// <param name="title">Заголовок сообщения.</param>
    /// <param name="error">Сбой.</param>
    internal static void Report(string title, Exception error) =>
        RunCore(() => ReportAsync(title, error));

    /// <summary>
    /// Показывает сбой на текущей странице. Сам показ тоже под перехватом: окно
    /// сообщения зависит от состояния навигации, и его сбой превратился бы ровно
    /// в то молчаливое падение, от которого этот тип и заведён.
    /// </summary>
    private static async Task ReportAsync(string title, Exception error)
    {
        // Журнал устройства, а не Debug.WriteLine: тот из релизной сборки вырезан
        // компилятором, и на телефоне от сбоя не оставалось бы ни строки
        Android.Util.Log.Error(LogTag, error.ToString());

        // Журнал устройства с телефона не достать, а отчёт о сбое уходит кнопкой «Поделиться».
        // Нарушенное правило — не сбой: в след идёт только его имя
        if (error is DomainException rule)
        {
            CrashCatcher.Trail?.RuleBroken(rule.Invariant);
        }
        else
        {
            CrashCatcher.Caught(error);
        }

        try
        {
            if (Shell.Current?.CurrentPage is { } page)
            {
                await page.DisplayAlertAsync(title, Explain(error), UiTexts.CommonClose);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Показать было нечем. Роняя процесс отсюда, мы потеряли бы и исходный сбой
            Android.Util.Log.Error(LogTag, failure.ToString());
            CrashCatcher.Caught(failure);
        }
    }

    /// <summary>
    /// Что сказать пользователю. Нарушенное доменное правило и сорванная миграция
    /// объясняют себя сами — их сообщения писались для экрана. Всё остальное
    /// пришло из библиотек, и показывать это значит пугать текстом, по которому
    /// всё равно нечего сделать.
    /// </summary>
    private static string Explain(Exception error) => error switch
    {
        DomainException or DatabaseMigrationException => error.Message,
        _ => UiTexts.ErrorUnexpected
    };
}
