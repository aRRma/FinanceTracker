namespace Finance.Application.Features.Report;

/// <summary>
/// Счета для выбора счетов отчёта. Модель представления зовёт его напрямую.
/// </summary>
public interface IReportAccountsQuery
{
    /// <summary>
    /// Читает все неудалённые счета в порядке показа: в каждой валюте сначала
    /// незаблокированные без признака «скрытый», затем накопления, затем заблокированные;
    /// внутри — порядок справочника. Порядок один на экран выбора и знаки набора, чтобы
    /// знаки стояли так же, как строки.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<ReportAccount>> ReadAsync(CancellationToken cancellationToken = default);
}
