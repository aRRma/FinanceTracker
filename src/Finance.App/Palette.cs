namespace Finance.App;

/// <summary>
/// Токены палитры темы для кода, которому цвет нужен значением, а не привязкой в разметке.
/// </summary>
/// <remarks>
/// Нет токена — <see langword="null"/>: пропуск в палитре виден на экране, а не роняет его.
/// </remarks>
internal static class Palette
{
    /// <summary>
    /// Цвет токена с суффиксом темы: «CardLight», «AccentDark».
    /// </summary>
    /// <param name="key">Имя токена в <c>Colors.xaml</c>.</param>
    public static Color? Find(string key) =>
        ControlsApplication.Current?.Resources.TryGetValue(key, out object? value) is true ? value as Color : null;

    /// <summary>
    /// Цвет токена в нынешней теме приложения: «Card» в тёмной теме — «CardDark».
    /// </summary>
    /// <param name="name">Имя токена без суффикса темы.</param>
    public static Color? Now(string name) =>
        Find(name + (ControlsApplication.Current?.RequestedTheme is AppTheme.Dark ? "Dark" : "Light"));
}
