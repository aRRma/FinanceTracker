using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Операции одного счёта с обеих его сторон — списания и зачисления.
/// Две выборки объединяются через <c>UNION ALL</c>, а не через <c>OR</c> по двум
/// колонкам: дизъюнкцию не берёт ни один из двух индексов, и при пятидесяти
/// тысячах операций проверка при правке счёта превращается в полный проход.
/// </summary>
internal static class AccountTransactions
{
    /// <summary>
    /// Была ли по счёту хоть одна операция когда-либо — включая мягко удалённые:
    /// удалённая записана в валюте счёта, и смена валюты переписала бы её задним числом.
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="accountKey">Ключ счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static Task<bool> ExistedEverAsync(
        FinanceDbContext context,
        Guid accountKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BothSides(context.Transactions.AsNoTracking().IgnoreQueryFilters(), accountKey)
            .AnyAsync(cancellationToken);
    }

    /// <summary>Дата самой ранней неудалённой операции по счёту; пусто — операций нет.</summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="accountKey">Ключ счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static Task<DateOnly?> EarliestOnAsync(
        FinanceDbContext context,
        Guid accountKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BothSides(context.Transactions.AsNoTracking(), accountKey)
            .Select(row => (DateOnly?)row.OccurredOn)
            .MinAsync(cancellationToken);
    }

    /// <summary>Операции по счёту списания и по счёту зачисления одним потоком: EF переводит это в <c>UNION ALL</c>.</summary>
    private static IQueryable<TransactionRow> BothSides(IQueryable<TransactionRow> transactions, Guid accountKey) =>
        transactions
            .Where(row => row.SourceAccountKey == accountKey)
            .Concat(transactions.Where(row => row.TargetAccountKey == accountKey));
}
