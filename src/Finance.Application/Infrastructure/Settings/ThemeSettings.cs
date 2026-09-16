namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Перевод темы в запись настройки и обратно. Тема хранится словом, а не числом
/// перечисления: порядок членов однажды поменяют, и сохранённая единица станет
/// другой темой молча.
/// </summary>
public static class ThemeSettings
{
    private const string SystemValue = "system";
    private const string LightValue = "light";
    private const string DarkValue = "dark";

    extension(Theme theme)
    {
        /// <summary>
        /// Как тема записывается в настройки.
        /// </summary>
        public string Stored => theme switch
        {
            Theme.Light => LightValue,
            Theme.Dark => DarkValue,
            _ => SystemValue
        };

        /// <summary>
        /// Название темы для экрана — им же подписана строка раздела «Ещё».
        /// </summary>
        public string Caption => theme switch
        {
            Theme.Light => "Светлая",
            Theme.Dark => "Тёмная",
            _ => "Как в системе"
        };
    }

    extension(Theme)
    {
        /// <summary>
        /// Читает тему из настройки. Незаданная и незнакомая запись — системная тема:
        /// отказ запуститься из-за настройки оформления был бы несоразмерен.
        /// </summary>
        /// <param name="stored">Записанное значение настройки.</param>
        public static Theme Parse(string? stored) => stored switch
        {
            LightValue => Theme.Light,
            DarkValue => Theme.Dark,
            _ => Theme.System
        };
    }
}
