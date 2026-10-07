using System.Globalization;
using Finance.Application.Texts;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Как дата выглядит на экране. Собрано в одном месте по той же причине, что
/// и формат суммы: разойдись шапка дня с датой в карточке операции, один
/// и тот же день читался бы как два разных.
/// </summary>
/// <remarks>
/// Шаблоны остаются здесь, а не уезжают в ресурсы текста: подпись переводится
/// словами, а порядок дня и месяца — правило языка, и подмена шаблона молча
/// показала бы не ту дату.
/// </remarks>
public static class DateText
{
    /// <summary>
    /// День без года: «25 августа». Год не пишется, когда он и так понятен.
    /// </summary>
    /// <param name="date">Дата.</param>
    public static string Day(DateOnly date) => date.ToString("d MMMM", UiCulture.Current);

    /// <summary>
    /// День с годом: «25 августа 2025».
    /// </summary>
    /// <param name="date">Дата.</param>
    public static string DayWithYear(DateOnly date) => date.ToString("d MMMM yyyy", UiCulture.Current);

    /// <summary>
    /// День, а год — только чужой: в списке за этот год он был бы шумом в каждой строке.
    /// </summary>
    /// <param name="date">Дата.</param>
    /// <param name="today">Сегодняшняя дата пользователя.</param>
    public static string DayWithYearIfOther(DateOnly date, DateOnly today) =>
        date.Year == today.Year ? Day(date) : DayWithYear(date);

    /// <summary>
    /// Момент в зоне пользователя: «Сегодня, 09:57», «Вчера, 21:12», «6 октября, 22:41», год — только чужой.
    /// </summary>
    /// <param name="moment">Момент.</param>
    /// <param name="zone">Часовой пояс пользователя.</param>
    /// <param name="today">Сегодняшняя дата пользователя.</param>
    public static string Moment(DateTimeOffset moment, TimeZoneInfo zone, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(zone);

        DateTimeOffset local = TimeZoneInfo.ConvertTime(moment, zone);
        DateOnly date = DateOnly.FromDateTime(local.DateTime);
        CultureInfo culture = UiCulture.Current;
        string day = date == today ? UiTexts.TransactionToday
            : date == today.AddDays(-1) ? UiTexts.TransactionYesterday
            : DayWithYearIfOther(date, today);

        return string.Create(culture, $"{day}, {local.ToString("HH:mm", culture)}");
    }

    /// <summary>
    /// Месяц с годом строчными: «август 2026».
    /// </summary>
    /// <param name="date">Любой день месяца.</param>
    public static string MonthWithYear(DateOnly date) => date.ToString("MMMM yyyy", UiCulture.Current);

    /// <summary>
    /// Месяц с годом заголовком: «Август 2026». Прописная буква ставится здесь,
    /// а не берётся у культуры: русские названия месяцев строчные всегда.
    /// </summary>
    /// <param name="date">Любой день месяца.</param>
    public static string MonthWithYearTitle(DateOnly date)
    {
        CultureInfo culture = UiCulture.Current;
        string name = date.ToString("MMMM", culture);

        return string.Create(culture, $"{char.ToUpper(name[0], culture)}{name.AsSpan(1)} {date.Year}");
    }
}
