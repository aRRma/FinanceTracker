using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Report;

/// <summary>
/// Суммы отчёта. Что входит и что нет, задано одним сборщиком операций месяца:
/// переводы, счета вне сумм, чужие валюты и категории вне отчётов отсеиваются
/// в нём, и три уровня по построению считают одно и то же.
/// </summary>
public sealed class ReportQuery : IReportQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>Создаёт запрос.</summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public ReportQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportTotal>> ReadGroupsAsync(
        ReportMonth month,
        CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Bucket> buckets = await Groups(context, month)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToTotals(buckets);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportTotal>> ReadSubcategoriesAsync(
        Guid groupKey,
        ReportMonth month,
        CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Bucket> buckets = await Subcategories(context, groupKey, month)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToTotals(buckets);
    }

    /// <summary>
    /// Суммы по подкатегориям одной группы за месяц. Соединение с группой остаётся:
    /// у подкатегории нет своего вида, и нужен флаг «вне отчётов» самой группы.
    /// </summary>
    internal static IQueryable<Bucket> Subcategories(FinanceDbContext context, Guid groupKey, ReportMonth month) =>
        from row in Counted(context, month)
        join category in context.Categories.AsNoTracking() on row.CategoryKey equals category.Key
        join parent in context.Categories.AsNoTracking() on category.ParentKey equals parent.Key
        where category.ParentKey == groupKey
              && !category.ExcludeFromReports
              && !parent.ExcludeFromReports
        group row.Amount by new { category.Key, category.Name, category.Icon, parent.Kind } into bucket
        orderby bucket.Sum() descending
        select new Bucket
        {
            Key = bucket.Key.Key,
            Name = bucket.Key.Name,
            Icon = bucket.Key.Icon,
            Kind = bucket.Key.Kind,
            Total = bucket.Sum()
        };

    /// <summary>
    /// Суммы по группам за месяц. Открыт тестам, чтобы сверить SQL и план запроса:
    /// сложение, уехавшее из базы в память, тестом на числа не отличить.
    /// </summary>
    internal static IQueryable<Bucket> Groups(FinanceDbContext context, ReportMonth month) =>
        from row in Counted(context, month)
        join category in context.Categories.AsNoTracking() on row.CategoryKey equals category.Key
        join parent in context.Categories.AsNoTracking() on category.ParentKey equals parent.Key
        // Флаг «вне отчётов» проверяется на обоих уровнях: он ставится и группе
        // целиком, и одной подкатегории, и проверка одного уровня пропустила бы другой
        where !category.ExcludeFromReports && !parent.ExcludeFromReports
        group row.Amount by new { parent.Key, parent.Name, parent.Icon, parent.Kind } into bucket
        orderby bucket.Sum() descending
        select new Bucket
        {
            Key = bucket.Key.Key,
            Name = bucket.Key.Name,
            Icon = bucket.Key.Icon,
            Kind = bucket.Key.Kind,
            Total = bucket.Sum()
        };

    /// <summary>
    /// Операции месяца, попадающие в суммы: по счетам в рублях без «скрыть из расчётов».
    /// Перевод отсеян и соединением с категорией — её у перевода нет, — но условие
    /// написано явно: правило «переводы не входят в отчёт» слишком дорого, чтобы
    /// держаться на чужом инварианте.
    /// </summary>
    private static IQueryable<TransactionRow> Counted(FinanceDbContext context, ReportMonth month)
    {
        DateOnly first = month.First;
        DateOnly last = month.Last;

        return from row in context.Transactions.AsNoTracking()
               where row.Kind != TransactionKind.Transfer
                     && row.OccurredOn >= first
                     && row.OccurredOn <= last
               join account in CountedAccounts.Of(context) on row.SourceAccountKey equals account.Key
               select row;
    }

    /// <summary>
    /// Вид у группы обязан быть заполнен. Пустой — испорченные данные: строка
    /// молча пропала бы из обоих списков, а операции на ней остались.
    /// </summary>
    private static List<ReportTotal> ToTotals(List<Bucket> buckets)
    {
        List<ReportTotal> totals = new(buckets.Count);

        foreach (Bucket bucket in buckets)
        {
            totals.Add(new ReportTotal
            {
                Key = bucket.Key,
                Name = bucket.Name,
                Icon = bucket.Icon,
                Kind = bucket.Kind ?? throw new InvalidOperationException($"У группы «{bucket.Name}» не задан вид"),
                Total = Money.Restore(bucket.Total, Currency.RUB)
            });
        }

        return totals;
    }

    /// <summary>Что база отдаёт на строку уровня. Сумма — голый <c>decimal</c>: <c>Money</c> в SQL не собрать.</summary>
    internal sealed class Bucket
    {
        public required Guid Key { get; init; }

        public required string Name { get; init; }

        public required string Icon { get; init; }

        public required CategoryKind? Kind { get; init; }

        public required decimal Total { get; init; }
    }
}
