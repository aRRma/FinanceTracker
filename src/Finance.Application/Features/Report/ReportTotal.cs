using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Features.Report;

/// <summary>
/// Строка уровня отчёта: группа на первом, подкатегория на втором. Сумма
/// в единицах своего вида, знак задаёт вид, как и везде в проекте, поэтому обычно она
/// положительна. Но у универсальной группы возвраты вычитаются из расхода, и итог бывает
/// отрицательным; порядок «по убыванию суммы» считается в памяти по сведённому итогу.
/// </summary>
public sealed record ReportTotal
{
    /// <summary>
    /// Ключ группы или подкатегории — по нему проваливаются на уровень ниже.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Название.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Вид: расход или доход. На первом уровне по нему делится список.
    /// </summary>
    public required CategoryKind Kind { get; init; }

    /// <summary>
    /// Сумма за месяц в валюте счетов отчёта; отрицательная, если возвратов у универсальной группы больше трат.
    /// </summary>
    public required Money Total { get; init; }
}
