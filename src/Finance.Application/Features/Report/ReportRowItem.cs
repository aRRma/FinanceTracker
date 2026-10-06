using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Features.Report;

/// <summary>
/// Строка отчёта: название, сумма со знаком, доля в процентах и полоса под ней.
/// </summary>
/// <param name="Key">Ключ группы или подкатегории — по нему проваливаются ниже.</param>
/// <param name="Name">Название.</param>
/// <param name="Icon">Ключ значка.</param>
/// <param name="Amount">Сумма со знаком: расход минусом, доход плюсом.</param>
/// <param name="Share">Доля в процентах, целыми: «37%». Пустая у строки, где возвратов больше, чем трат.</param>
/// <param name="Fraction">Та же доля долей единицы — ширина полосы.</param>
/// <param name="IsExpense">Сумма со знаком отрицательна — красится цветом расхода, иначе цветом дохода.</param>
public sealed record ReportRowItem(
    Guid Key,
    string Name,
    string Icon,
    string Amount,
    string Share,
    double Fraction,
    bool IsExpense)
{
    /// <summary>
    /// Полоса тоньше этого не видна, а невидимая полоса читается как её отсутствие.
    /// </summary>
    private const double MinimalFraction = 0.02;

    /// <summary>
    /// Доля есть — показываются проценты и полоса.
    /// </summary>
    public bool HasShare => Share.Length > 0;

    /// <summary>
    /// Собирает строку. Доли округляются каждая сама по себе и в сумме могут дать
    /// 99 или 101: подогнанное число не совпало бы с суммой, поделённой на базу.
    /// </summary>
    /// <remarks>
    /// Строка универсальной группы, где возвратов за месяц больше, чем трат, сводится
    /// в минус своего вида. Доли у неё нет: «−20%» рядом с полосой читался бы как
    /// бессмыслица. Сумма при этом показывается честно — со знаком и цветом обратного вида.
    /// </remarks>
    /// <param name="row">Строка уровня.</param>
    /// <param name="shareBase">База доли уровня — <see cref="ShareBase"/>.</param>
    public static ReportRowItem From(ReportTotal row, Money shareBase)
    {
        ArgumentNullException.ThrowIfNull(row);

        Money signed = Signed(row.Total, row.Kind);
        bool hasShare = row.Total.Amount > 0m && shareBase.Amount > 0m;
        double fraction = hasShare ? (double)(row.Total.Amount / shareBase.Amount) : 0d;
        int percent = (int)Math.Round(fraction * 100d, MidpointRounding.AwayFromZero);

        return new ReportRowItem(
            row.Key,
            row.Name,
            row.Icon,
            signed.DisplaySigned,
            hasShare ? $"{percent}%" : string.Empty,
            hasShare ? Math.Max(fraction, MinimalFraction) : 0d,
            signed.Amount < 0m);
    }

    /// <summary>
    /// База долей уровня — сумма строк с положительным итогом, а не итог уровня.
    /// Строка, ушедшая в минус возвратами, уменьшила бы итог, и доли остальных
    /// перевалили бы в сумме за сотню.
    /// </summary>
    /// <param name="rows">Строки уровня одного вида.</param>
    /// <param name="currency">Валюта отчёта: у уровня без строк базе больше неоткуда её взять.</param>
    public static Money ShareBase(IEnumerable<ReportTotal> rows, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(rows);

        Money sum = Money.Zero(currency);

        foreach (ReportTotal row in rows)
        {
            if (row.Total.Amount > 0m)
            {
                sum += row.Total;
            }
        }

        return sum;
    }

    /// <summary>
    /// Расход показывается минусом: в базе суммы всегда положительны, знак задаёт вид.
    /// </summary>
    /// <param name="total">Положительная сумма.</param>
    /// <param name="kind">Вид строки.</param>
    public static Money Signed(Money total, CategoryKind kind) =>
        kind is CategoryKind.Expense ? -total : total;
}
