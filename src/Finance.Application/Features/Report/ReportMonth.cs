using System.Globalization;
using Finance.Application.Infrastructure;

namespace Finance.Application.Features.Report;

/// <summary>
/// Месяц отчёта и его границы. Отдельный тип, потому что «последнее число» —
/// не константа, а границы нужны трём запросам: разъехавшись, они дали бы
/// три разных ответа на один вопрос.
/// </summary>
/// <remarks>
/// Даты календарные, <see cref="DateOnly"/>: месяц, посчитанный от <c>UtcNow</c>,
/// каждый вечер первого числа показывал бы прошлый. Свойства без <c>init</c>
/// намеренно: <c>with</c> собрал бы месяц, начинающийся не с первого числа.
/// </remarks>
public readonly record struct ReportMonth
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private ReportMonth(DateOnly first) => First = first;

    /// <summary>
    /// Первое число месяца — нижняя граница, включительно.
    /// </summary>
    public DateOnly First { get; }

    /// <summary>
    /// Последнее число месяца — верхняя граница, включительно.
    /// </summary>
    public DateOnly Last => First.AddMonths(1).AddDays(-1);

    /// <summary>
    /// Предыдущий месяц.
    /// </summary>
    public ReportMonth Previous => new(First.AddMonths(-1));

    /// <summary>
    /// Следующий месяц.
    /// </summary>
    public ReportMonth Next => new(First.AddMonths(1));

    /// <summary>
    /// Шапка переключателя: «Август 2026». Без дня формат даёт именительный падеж.
    /// </summary>
    public string Title
    {
        get
        {
            string name = First.ToString("MMMM", Russian);

            return string.Create(Russian, $"{char.ToUpper(name[0], Russian)}{name.AsSpan(1)} {First.Year}");
        }
    }

    /// <summary>
    /// Подпись внутри строки: «август 2026».
    /// </summary>
    public string Caption => First.ToString("MMMM yyyy", Russian);

    /// <summary>
    /// Месяц, в который попадает дата.
    /// </summary>
    /// <param name="date">Любая дата месяца.</param>
    public static ReportMonth Of(DateOnly date) => new(new DateOnly(date.Year, date.Month, 1));

    /// <summary>
    /// Текущий месяц пользователя — по его дате, а не по UTC.
    /// </summary>
    /// <param name="clock">Часы приложения.</param>
    public static ReportMonth Current(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return Of(clock.Today);
    }

    /// <summary>
    /// Месяц строкой для параметра перехода: «2026-08».
    /// </summary>
    public override string ToString() => First.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>
    /// Разбирает месяц из параметра перехода в виде «2026-08».
    /// </summary>
    /// <param name="value">Строка параметра.</param>
    /// <param name="month">Разобранный месяц.</param>
    public static bool TryParse(string? value, out ReportMonth month)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly first))
        {
            month = Of(first);
            return true;
        }

        month = default;
        return false;
    }
}
