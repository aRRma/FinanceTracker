namespace Finance.App;

/// <summary>
/// Переходы по касанию. Пока один переход не завершился, второй не начинается:
/// касание, пришедшее вдогонку первому, иначе клало в стек вторую копию той же
/// страницы, а «..» с экрана выбора уводило на два шага — вместе с формой,
/// ради которой выбирали.
/// </summary>
internal static class Navigator
{
    // Все переходы идут из потока интерфейса, поэтому хватает простого признака
    private static bool _navigating;

    /// <summary>
    /// Переходит по маршруту из обработчика события; сбой показывается через <see cref="Guarded"/>.
    /// </summary>
    /// <param name="route">Маршрут <c>Shell</c>.</param>
    internal static void Go(string route) => Guarded.Run(() => GoAsync(route));

    /// <summary>
    /// Переходит по маршруту, если другой переход не идёт прямо сейчас.
    /// </summary>
    /// <param name="route">Маршрут <c>Shell</c>.</param>
    internal static async Task GoAsync(string route)
    {
        if (_navigating)
        {
            return;
        }

        _navigating = true;

        try
        {
            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            _navigating = false;
        }
    }
}
