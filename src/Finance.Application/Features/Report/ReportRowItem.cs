using Finance.Application.Infrastructure;
using Finance.Domain;

namespace Finance.Application.Features.Report;

/// <summary>Строка отчёта: название, сумма со знаком, доля в процентах и полоса под ней.</summary>
/// <param name="Key">Ключ группы или подкатегории — по нему проваливаются ниже.</param>
/// <param name="Name">Название.</param>
/// <param name="Icon">Ключ значка.</param>
/// <param name="Amount">Сумма со знаком: расход минусом, доход плюсом.</param>
/// <param name="Share">Доля в процентах, целыми: «37%».</param>
/// <param name="Fraction">Та же доля долей единицы — ширина полосы.</param>
/// <param name="IsExpense">Расход — красится смысловым цветом, доход другим.</param>
public sealed record ReportRowItem(
    Guid Key,
    string Name,
    string Icon,
    string Amount,
    string Share,
    double Fraction,
    bool IsExpense)
{
    /// <summary>Полоса тоньше этого не видна, а невидимая полоса читается как её отсутствие.</summary>
    private const double MinimalFraction = 0.02;

    /// <summary>
    /// Собирает строку. Знаменатель доли — итог уровня: месяца на первом, группы
    /// на втором. Доли округляются каждая сама по себе и в сумме могут дать 99 или 101:
    /// подогнанное число не совпало бы с суммой, поделённой на итог.
    /// </summary>
    /// <param name="row">Строка уровня.</param>
    /// <param name="whole">Итог уровня, от которого считается доля.</param>
    public static ReportRowItem From(ReportTotal row, Money whole)
    {
        ArgumentNullException.ThrowIfNull(row);

        // Итог нулевым не бывает — строка без суммы в список не попадает, —
        // но делению на ноль верить нельзя ни при каких обстоятельствах
        double fraction = whole.Amount is 0m ? 0d : (double)(row.Total.Amount / whole.Amount);
        int percent = (int)Math.Round(fraction * 100d, MidpointRounding.AwayFromZero);

        return new ReportRowItem(
            row.Key,
            row.Name,
            row.Icon,
            Signed(row.Total, row.Kind).DisplaySigned,
            $"{percent}%",
            Math.Max(fraction, MinimalFraction),
            row.Kind is CategoryKind.Expense);
    }

    /// <summary>Расход показывается минусом: в базе суммы всегда положительны, знак задаёт вид.</summary>
    /// <param name="total">Положительная сумма.</param>
    /// <param name="kind">Вид строки.</param>
    public static Money Signed(Money total, CategoryKind kind) =>
        kind is CategoryKind.Expense ? -total : total;
}
