namespace Finance.Application.Features.Report;

/// <summary>
/// Суммы отчёта за месяц. Считает база: поднимать операции месяца в память ради
/// сложения значило бы тянуть тысячу строк там, где нужен десяток чисел.
/// </summary>
public interface IReportQuery
{
    /// <summary>
    /// Первый уровень: группы месяца с суммами, по убыванию. Оба вида сразу —
    /// переключатель расходов и доходов нажимают подряд, и запрос на каждое
    /// нажатие был бы обращением к базе ради того, что уже прочитано.
    /// </summary>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<ReportTotal>> ReadGroupsAsync(ReportMonth month, CancellationToken cancellationToken = default);

    /// <summary>Второй уровень: подкатегории группы с суммами за месяц, по убыванию.</summary>
    /// <param name="groupKey">Ключ группы.</param>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<ReportTotal>> ReadSubcategoriesAsync(Guid groupKey, ReportMonth month, CancellationToken cancellationToken = default);
}
