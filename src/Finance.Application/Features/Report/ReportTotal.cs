using Finance.Domain;

namespace Finance.Application.Features.Report;

/// <summary>
/// Строка уровня отчёта: группа на первом, подкатегория на втором. Сумма
/// положительна — знак задаёт вид, как и везде в проекте: так «по убыванию суммы»
/// совпадает с порядком базы буквально, а доля считается делением без возни со знаками.
/// </summary>
public sealed record ReportTotal
{
    /// <summary>Ключ группы или подкатегории — по нему проваливаются на уровень ниже.</summary>
    public required Guid Key { get; init; }

    /// <summary>Название.</summary>
    public required string Name { get; init; }

    /// <summary>Ключ значка.</summary>
    public required string Icon { get; init; }

    /// <summary>Вид: расход или доход. На первом уровне по нему делится список.</summary>
    public required CategoryKind Kind { get; init; }

    /// <summary>Сумма за месяц в рублях, всегда положительная.</summary>
    public required Money Total { get; init; }
}
