namespace Finance.Application.Features.Report;

/// <summary>
/// Суммы отчёта за месяц. Считает база: поднимать операции месяца в память ради
/// сложения значило бы тянуть тысячу строк там, где нужен десяток чисел.
/// </summary>
/// <remarks>
/// Каждое чтение получает счета отчёта параметром, а не берёт выбор само: три уровня
/// и подсказка пустого состояния обязаны считать по одному набору, и тест задаёт его явно.
/// </remarks>
public interface IReportQuery
{
    /// <summary>
    /// Первый уровень: группы месяца с суммами, по убыванию. Оба вида сразу —
    /// переключатель расходов и доходов нажимают подряд, и запрос на каждое
    /// нажатие был бы обращением к базе ради того, что уже прочитано.
    /// </summary>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="accounts">Счета отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<ReportTotal>> ReadGroupsAsync(ReportMonth month, ReportAccounts accounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Второй уровень: подкатегории группы с суммами за месяц, по убыванию.
    /// </summary>
    /// <param name="groupKey">Ключ группы.</param>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="accounts">Счета отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<ReportTotal>> ReadSubcategoriesAsync(Guid groupKey, ReportMonth month, ReportAccounts accounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Третий уровень: операции подкатегории за месяц, от новых к старым, плоским
    /// списком. Без страниц: месяц одной подкатегории — десятки строк, не тысячи.
    /// </summary>
    /// <param name="subcategoryKey">Ключ подкатегории.</param>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="accounts">Счета отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<ReportTransaction>> ReadTransactionsAsync(Guid subcategoryKey, ReportMonth month, ReportAccounts accounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Есть ли за месяц операции, не попавшие в суммы: по счетам вне набора.
    /// Пустому отчёту это говорит, советовать ли сменить месяц: если операции
    /// месяца есть, другой месяц не поможет.
    /// </summary>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="accounts">Счета отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<bool> HasUncountedAsync(ReportMonth month, ReportAccounts accounts, CancellationToken cancellationToken = default);
}
