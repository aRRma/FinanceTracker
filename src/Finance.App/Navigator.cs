using System.Runtime.CompilerServices;

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
    /// <param name="file">Файл обработчика — для следа действий; подставляет компилятор.</param>
    /// <param name="member">Имя обработчика — для следа действий; подставляет компилятор.</param>
    /// <remarks>
    /// Имя вызвавшего передаётся дальше: иначе след записал бы каждый переход как <c>Navigator.Go</c>,
    /// и не было бы видно, какую строку нажали.
    /// </remarks>
    internal static void Go(
        string route,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "") =>
        Guarded.Run(() => GoAsync(route), file, member);

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
