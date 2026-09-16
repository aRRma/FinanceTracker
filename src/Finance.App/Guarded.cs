using System.Diagnostics;
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
    /// Выполняет тело обработчика, показывая сбой вместо молчаливого закрытия окна.
    /// </summary>
    /// <param name="action">Тело обработчика.</param>
    internal static async void Run(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await ReportAsync(error);
        }
    }

    /// <summary>
    /// Показывает сбой на текущей странице. Сам показ тоже под перехватом: окно
    /// сообщения зависит от состояния навигации, и его сбой превратился бы ровно
    /// в то молчаливое падение, от которого этот тип и заведён.
    /// </summary>
    private static async Task ReportAsync(Exception error)
    {
        // Текст исключения написан для того, кто читает код. Пользователю он
        // говорит или ничего, или лишнее, поэтому наружу выходят только те
        // сообщения, которые для него и составлялись
        Debug.WriteLine(error);

        try
        {
            if (Shell.Current?.CurrentPage is { } page)
            {
                await page.DisplayAlertAsync("Не удалось выполнить действие", Explain(error), "Закрыть");
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Показать было нечем. Роняя процесс отсюда, мы потеряли бы и исходный сбой
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
        _ => "Что-то пошло не так. Попробуйте ещё раз; подробности — в журнале устройства."
    };
}
