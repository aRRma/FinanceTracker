namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Событие жизни приложения для следа действий. В отчёте пишется именем.
/// </summary>
public enum LifecycleEvent
{
    /// <summary>
    /// Событие не задано. Настоящим значением не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Процесс запущен.
    /// </summary>
    Started = 1,

    /// <summary>
    /// Приложение ушло в фон.
    /// </summary>
    Stopped = 2,

    /// <summary>
    /// Приложение вернулось из фона.
    /// </summary>
    Restarted = 3,

    /// <summary>
    /// Сменилась тема — рядом с этим однажды упала отрисовка значков.
    /// </summary>
    ThemeChanged = 4,

    /// <summary>
    /// Система просит освободить память: следом процесс могут убить в фоне.
    /// </summary>
    TrimMemory = 5,
}
