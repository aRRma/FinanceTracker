using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Report;

/// <summary>
/// Чтение счетов для отчёта: одна проекция таблицы счетов, без операций.
/// </summary>
public sealed class ReportAccountsQuery : IReportAccountsQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public ReportAccountsQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportAccount>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Порядок разделов задаёт база: значение выражения сортировки — номер раздела
        var rows = await context.Accounts
            .AsNoTracking()
            .OrderBy(static row => row.IsClosed ? 2 : row.ExcludedFromTotals ? 1 : 0)
            .ThenBy(static row => row.SortOrder)
            .ThenBy(static row => row.Name)
            .Select(static row => new
            {
                row.Key,
                row.Name,
                row.Type,
                row.Color,
                row.Icon,
                row.Currency,
                row.ExcludedFromTotals,
                row.IsClosed
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ReportAccount> accounts = new(rows.Count);

        foreach (var row in rows)
        {
            accounts.Add(new ReportAccount
            {
                Key = row.Key,
                Name = row.Name,
                Color = row.Color,
                Icon = AccountIcon.For(row.Type, row.ExcludedFromTotals, row.Icon),
                Currency = row.Currency,
                IsSavings = row.ExcludedFromTotals,
                IsClosed = row.IsClosed
            });
        }

        return accounts;
    }
}
