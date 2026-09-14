namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Оформление приложения. Три состояния, а не переключатель «тёмная да/нет»:
/// вернуться к системной теме после ручного выбора иначе невозможно.
/// </summary>
public enum Theme
{
    /// <summary>Как в системе. Ею же оборачивается незаданная и незнакомая настройка.</summary>
    System,

    /// <summary>Светлая независимо от системной.</summary>
    Light,

    /// <summary>Тёмная независимо от системной.</summary>
    Dark
}
