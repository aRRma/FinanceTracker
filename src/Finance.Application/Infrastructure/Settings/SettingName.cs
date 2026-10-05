namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Имена локальных настроек. Строками по месту они разошлись бы в опечатке,
/// а прочитанная не тем именем настройка выглядит просто как несохранённая.
/// </summary>
public static class SettingName
{
    /// <summary>
    /// Номер применённой версии стартового набора. Повторно набор не применяется никогда.
    /// </summary>
    public const string PresetVersion = "preset.version";

    /// <summary>
    /// Идентификатор часовой зоны пользователя, если он задал её вручную.
    /// </summary>
    public const string TimeZoneId = "time_zone.id";

    /// <summary>
    /// Тема оформления: системная, светлая или тёмная.
    /// </summary>
    public const string Theme = "theme";

    /// <summary>
    /// Ключ счёта, явно выбранного счётом по умолчанию. Нет строки — счёт по умолчанию верхний в списке.
    /// </summary>
    public const string DefaultAccountKey = "transactions.default_account_key";
}
