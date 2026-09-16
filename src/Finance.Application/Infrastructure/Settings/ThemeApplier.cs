namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Доставляет выбранную тему платформе. Прикладной слой не знает про MAUI,
/// поэтому само переключение подаётся снаружи — вместе со способом попасть
/// в поток интерфейса: тема меняет оформление окна, а это делается только оттуда.
/// </summary>
public sealed class ThemeApplier
{
    private readonly Action<Theme>? _apply;
    private readonly Action<Action> _dispatch;

    /// <summary>
    /// Создаёт применение темы.
    /// </summary>
    /// <param name="apply">Как платформа переключает оформление. Пусто — переключать нечего, так работают тесты.</param>
    /// <param name="dispatch">Как выполнить действие в потоке интерфейса. Пусто — выполняется на месте.</param>
    public ThemeApplier(Action<Theme>? apply = null, Action<Action>? dispatch = null)
    {
        _apply = apply;
        _dispatch = dispatch ?? (static action => action());
    }

    /// <summary>
    /// Применяет тему. Зовётся и при запуске, и при выборе: подготовка идёт
    /// в фоновом потоке, и без перехода в поток интерфейса первый вызов уронил бы окно.
    /// </summary>
    /// <param name="theme">Выбранная тема.</param>
    public void Apply(Theme theme)
    {
        if (_apply is { } apply)
        {
            _dispatch(() => apply(theme));
        }
    }
}
