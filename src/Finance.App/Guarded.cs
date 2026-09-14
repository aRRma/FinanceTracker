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
        try
        {
            if (Shell.Current?.CurrentPage is { } page)
            {
                await page.DisplayAlertAsync("Не удалось выполнить действие", error.Message, "Закрыть");
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Показать было нечем. Роняя процесс отсюда, мы потеряли бы и исходный сбой
        }
    }
}
