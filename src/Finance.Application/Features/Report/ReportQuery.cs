using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Enums;
using Finance.Domain.Values;
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

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportTransaction>> ReadTransactionsAsync(
        Guid subcategoryKey,
        ReportMonth month,
        CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Line> lines = await Transactions(context, subcategoryKey, month)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ReportTransaction> items = new(lines.Count);

        foreach (Line line in lines)
        {
            // Знак приписывается здесь: в базе суммы всегда положительны
            decimal signed = line.Kind is TransactionKind.Income ? line.Amount : -line.Amount;

            items.Add(new ReportTransaction
            {
                Key = line.Key,
                OccurredOn = line.OccurredOn,
                Amount = Money.Restore(signed, Currency.RUB),
                AccountName = line.AccountName,
                Place = line.Place,
                Note = line.Note
            });
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<bool> HasUncountedAsync(ReportMonth month, CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateOnly first = month.First;
        DateOnly last = month.Last;

        // Левое соединение с учитываемыми счетами: операция без пары — как раз та,
        // что в суммы не вошла
        return await (
                from row in context.Transactions.AsNoTracking()
                where row.Kind != TransactionKind.Transfer
                      && row.OccurredOn >= first
                      && row.OccurredOn <= last
                join account in CountedAccounts.Of(context) on row.SourceAccountKey equals account.Key into counted
                from account in counted.DefaultIfEmpty()
                where account == null
                select row.Key)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Операции подкатегории за месяц. Порядок тот же, что в ленте: дата, момент
    /// записи, ключ — иначе две операции одного дня менялись бы местами между заходами.
    /// Соединение с местом левое: место могло быть удалено из справочника,
    /// и такая операция показывается как операция без места. Открыт тестам — сверяется план.
    /// </summary>
    internal static IQueryable<Line> Transactions(FinanceDbContext context, Guid subcategoryKey, ReportMonth month) =>
        from row in Counted(context, month)
        where row.CategoryKey == subcategoryKey
        join account in context.Accounts.AsNoTracking() on row.SourceAccountKey equals account.Key
        join place in context.Places.AsNoTracking() on row.PlaceKey equals place.Key into places
        from place in places.DefaultIfEmpty()
        orderby row.OccurredOn descending, row.CreatedAtUtc descending, row.Key descending
        select new Line
        {
            Key = row.Key,
            Kind = row.Kind,
            OccurredOn = row.OccurredOn,
            Amount = row.Amount,
            AccountName = account.Name,
            Place = place != null ? place.Name : null,
            Note = row.Note
        };

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
        group row.Amount by new
        {
            category.Key, category.Name, category.Icon, GroupKind = parent.Kind, OperationKind = row.Kind
        } into bucket
        select new Bucket
        {
            Key = bucket.Key.Key,
            Name = bucket.Key.Name,
            Icon = bucket.Key.Icon,
            Kind = bucket.Key.GroupKind,
            OperationKind = bucket.Key.OperationKind,
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
        group row.Amount by new
        {
            parent.Key, parent.Name, parent.Icon, GroupKind = parent.Kind, OperationKind = row.Kind
        } into bucket
        select new Bucket
        {
            Key = bucket.Key.Key,
            Name = bucket.Key.Name,
            Icon = bucket.Key.Icon,
            Kind = bucket.Key.GroupKind,
            OperationKind = bucket.Key.OperationKind,
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
    /// Сводит суммы строки в одну: операции вида группы прибавляются, операции
    /// обратного вида вычитаются. Так возврат в магазине уменьшает расход статьи,
    /// а не заводит доход на пустом месте, — у универсальной группы в строке лежат
    /// оба вида. База отдаёт их порознь, потому что <c>SUM</c> со знаком внутри
    /// <c>CASE</c> потерял бы конвертер копеек, а порядок строк считается уже
    /// по сведённому итогу.
    /// </summary>
    /// <remarks>
    /// Вид у группы обязан быть заполнен. Пустой — испорченные данные: строка
    /// молча пропала бы из обоих списков, а операции на ней остались.
    /// </remarks>
    private static List<ReportTotal> ToTotals(List<Bucket> buckets)
    {
        Dictionary<Guid, ReportTotal> totals = new(buckets.Count);

        foreach (Bucket bucket in buckets)
        {
            CategoryKind kind = bucket.Kind
                                ?? throw new InvalidOperationException(Faults.GroupKindMissing(bucket.Name));

            bool own = (bucket.OperationKind is TransactionKind.Income) == (kind is CategoryKind.Income);
            Money signed = Money.Restore(own ? bucket.Total : -bucket.Total, Currency.RUB);

            totals[bucket.Key] = totals.TryGetValue(bucket.Key, out ReportTotal? seen)
                ? seen with { Total = seen.Total + signed }
                : new ReportTotal
                {
                    Key = bucket.Key,
                    Name = bucket.Name,
                    Icon = bucket.Icon,
                    Kind = kind,
                    Total = signed
                };
        }

        return [.. totals.Values.OrderByDescending(total => total.Total.Amount)];
    }

    /// <summary>
    /// Что база отдаёт на операцию третьего уровня.
    /// </summary>
    internal sealed class Line
    {
        public required Guid Key { get; init; }

        public required TransactionKind Kind { get; init; }

        public required DateOnly OccurredOn { get; init; }

        public required decimal Amount { get; init; }

        public required string AccountName { get; init; }

        public string? Place { get; init; }

        public string? Note { get; init; }
    }

    /// <summary>
    /// Что база отдаёт на строку уровня. Сумма — голый <c>decimal</c>: <c>Money</c> в SQL не собрать.
    /// </summary>
    internal sealed class Bucket
    {
        public required Guid Key { get; init; }

        public required string Name { get; init; }

        public required string Icon { get; init; }

        public required CategoryKind? Kind { get; init; }

        /// <summary>
        /// Вид операций этой суммы: у универсальной группы строк две, расходная
        /// и доходная, и сводятся они со знаком.
        /// </summary>
        public required TransactionKind OperationKind { get; init; }

        public required decimal Total { get; init; }
    }
}
