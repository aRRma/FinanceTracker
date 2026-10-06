namespace Finance.Application.Features.Settings.TimeZones;

/// <summary>
/// Зоны, которыми подписан список поясов, — по одной на крупный город.
/// </summary>
/// <remarks>
/// Порядок — приоритет: строка списка ставит первую зону своего смещения и
/// подписана первыми тремя городами, поэтому города России идут раньше соседей,
/// а соседи — раньше остального мира. Смещения на сегодня покрыты все, где живут люди.
/// </remarks>
public static class TimeZoneCities
{
    /// <summary>
    /// Идентификаторы зон в порядке приоритета. Названия — в <c>Texts/CityNames.resx</c>.
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } =
    [
        "Europe/Moscow",
        "Europe/Kaliningrad",
        "Europe/Samara",
        "Asia/Yekaterinburg",
        "Asia/Omsk",
        "Asia/Novosibirsk",
        "Asia/Krasnoyarsk",
        "Asia/Irkutsk",
        "Asia/Yakutsk",
        "Asia/Vladivostok",
        "Asia/Magadan",
        "Asia/Kamchatka",
        "Europe/Minsk",
        "Asia/Almaty",
        "Asia/Tashkent",
        "Asia/Bishkek",
        "Asia/Baku",
        "Asia/Tbilisi",
        "Asia/Yerevan",
        "Europe/Istanbul",
        "Europe/Berlin",
        "Europe/Paris",
        "Europe/London",
        "Europe/Helsinki",
        "Europe/Athens",
        "Africa/Cairo",
        "Africa/Lagos",
        "Atlantic/Reykjavik",
        "Atlantic/Azores",
        "Atlantic/Cape_Verde",
        "Asia/Dubai",
        "Asia/Tehran",
        "Asia/Kabul",
        "Asia/Kolkata",
        "Asia/Kathmandu",
        "Asia/Dhaka",
        "Asia/Yangon",
        "Asia/Bangkok",
        "Asia/Shanghai",
        "Asia/Singapore",
        "Australia/Eucla",
        "Asia/Tokyo",
        "Asia/Seoul",
        "Australia/Darwin",
        "Australia/Adelaide",
        "Australia/Sydney",
        "Australia/Brisbane",
        "Australia/Lord_Howe",
        "Pacific/Noumea",
        "Pacific/Auckland",
        "Pacific/Fiji",
        "Pacific/Chatham",
        "Pacific/Tongatapu",
        "Pacific/Kiritimati",
        "America/New_York",
        "America/Los_Angeles",
        "America/Chicago",
        "America/Mexico_City",
        "America/Denver",
        "America/Phoenix",
        "America/Sao_Paulo",
        "America/Argentina/Buenos_Aires",
        "America/Bogota",
        "America/Caracas",
        "America/Santiago",
        "America/Halifax",
        "America/St_Johns",
        "America/Noronha",
        "America/Anchorage",
        "Pacific/Marquesas",
        "Pacific/Honolulu",
        "Pacific/Pago_Pago"
    ];
}
