using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Чтение ленты. Строки собираются одним запросом с соединениями по счетам,
/// категориям и местам: подгружать названия по одной на строку — это N+1,
/// который на странице в полсотни строк даёт две сотни обращений к базе.
/// </summary>
/// <remarks>
/// Лента счёта — объединение двух выборок, по счёту списания и по счёту зачисления,
/// через <c>UNION ALL</c>: каждая идёт по своему индексу. Итоги дней считаются
/// на стороне базы отдельным сводным запросом по датам страницы.
/// </remarks>
public sealed class FeedQuery : IFeedQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public FeedQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<FeedPage> ReadAsync(
        Guid? accountKey,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // На одну строку больше, чем просили: так узнаётся, есть ли ещё,
        // без отдельного подсчёта всей ленты
        List<Projection> rows = await Compose(context, accountKey, after)
            .Take(take + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return FeedPage.Empty;
        }

        bool hasMore = rows.Count > take;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        // Строки идут от новых к старым: первая — самая поздняя дата, последняя — самая ранняя
        DateOnly latest = rows[0].OccurredOn;
        DateOnly earliest = rows[^1].OccurredOn;

        Dictionary<DateOnly, Money> totals = accountKey is { } viewed
            ? await AccountDayTotalsAsync(context, viewed, earliest, latest, cancellationToken).ConfigureAwait(false)
            : await LedgerDayTotalsAsync(context, earliest, latest, cancellationToken).ConfigureAwait(false);

        List<FeedItem> items = new(rows.Count);

        foreach (Projection row in rows)
        {
            items.Add(ToItem(row, accountKey));
        }

        Projection last = rows[^1];

        return new FeedPage(items, totals, hasMore, new FeedCursor(last.OccurredOn, last.CreatedAtUtc, last.Key));
    }

    /// <summary>
    /// Запрос ленты до разбиения на страницы: выборка после курсора, соединения, порядок.
    /// Открыт тестам, чтобы сверить SQL и план: лента счёта обязана идти через <c>UNION ALL</c>,
    /// а не через <c>OR</c> по двум колонкам, и страница после курсора — поиском по индексу.
    /// </summary>
    internal static IQueryable<Projection> Compose(FinanceDbContext context, Guid? accountKey, FeedCursor? after = null)
    {
        IQueryable<TransactionRow> transactions = context.Transactions.AsNoTracking();

        // Условие курсора повторяет порядок ленты: дата, момент создания, ключ — всё
        // по убыванию. «Не позже даты» стоит отдельным условием: с него база начинает
        // поиск с места в индексе, а не проходит индекс от начала, и это не зависит
        // от того, разберёт ли планировщик дизъюнкцию. Ставится до объединения
        // сторон, чтобы каждая выборка ленты счёта искала по своему индексу
        if (after is { OccurredOn: var day, CreatedAtUtc: var created, Key: var key })
        {
            transactions = transactions.Where(row =>
                row.OccurredOn <= day
                && (row.OccurredOn < day || row.CreatedAtUtc < created || (row.CreatedAtUtc == created && row.Key < key)));
        }

        IQueryable<TransactionRow> scope = accountKey is { } account
            ? AccountTransactions.BothSides(transactions, account)
            : transactions;

        return Project(context, scope)
            .OrderByDescending(row => row.OccurredOn)
            .ThenByDescending(row => row.CreatedAtUtc)
            .ThenByDescending(row => row.Key);
    }

    /// <summary>
    /// Собирает строку со стороны, с которой смотрят. Одна и та же запись перевода
    /// в ленте счёта зачисления — плюс в его валюте, в ленте счёта списания
    /// и в общей ленте — минус в валюте списания.
    /// </summary>
    private static FeedItem ToItem(Projection row, Guid? viewedAccount)
    {
        if (row.Kind is TransactionKind.Transfer && row.TargetAccountKey == viewedAccount)
        {
            return new FeedItem
            {
                Key = row.Key,
                Kind = row.Kind,
                OccurredOn = row.OccurredOn,
                Amount = Money.Restore(row.TargetAmount!.Value, row.TargetCurrency!.Value),
                AccountKey = row.TargetAccountKey!.Value,
                AccountName = row.TargetAccountName!,
                Account = row.TargetMark!,
                OtherAccount = row.SourceMark,
                Title = row.SourceAccountName,
                Note = row.Note
            };
        }

        decimal signed = row.Kind is TransactionKind.Income ? row.Amount : -row.Amount;

        return new FeedItem
        {
            Key = row.Key,
            Kind = row.Kind,
            OccurredOn = row.OccurredOn,
            Amount = Money.Restore(signed, row.SourceCurrency),
            AccountKey = row.SourceAccountKey,
            AccountName = row.SourceAccountName,
            Account = row.SourceMark,
            OtherAccount = row.TargetMark,

            // У перевода заголовком служит второй счёт, у остальных — подкатегория.
            // Категория без названия — ссылка на удалённую запись, чего по правилу
            // переноса в приёмник быть не должно; пустой заголовок честнее вымышленного
            Title = row.Kind is TransactionKind.Transfer ? row.TargetAccountName! : row.CategoryName ?? string.Empty,
            Group = row.GroupName,
            Place = row.PlaceName,
            Note = row.Note,
            Icon = row.CategoryIcon
        };
    }

    /// <summary>
    /// Итог дня общей ленты: в рублях, по счетам без признака «скрытый». Строки
    /// по счетам в других валютах в ленте видны, а в итог не входят: общей суммы
    /// по валютам не существует. Перевод входит как расход счёта списания, только
    /// если деньги ушли из учитываемых счетов — в валюту или на скрытый счёт.
    /// Перевод между двумя учитываемыми счетами итог не меняет: деньги остались
    /// в тех же суммах, просто на другом счёте.
    /// </summary>
    private static async Task<Dictionary<DateOnly, Money>> LedgerDayTotalsAsync(
        FinanceDbContext context,
        DateOnly earliest,
        DateOnly latest,
        CancellationToken cancellationToken)
    {
        var totals = await (
                from row in context.Transactions.AsNoTracking()
                where row.OccurredOn >= earliest && row.OccurredOn <= latest
                join source in CountedAccounts.Of(context, Currency.RUB) on row.SourceAccountKey equals source.Key
                join target in CountedAccounts.Of(context, Currency.RUB) on row.TargetAccountKey equals target.Key into targets
                from target in targets.DefaultIfEmpty()
                group new { row.Kind, row.Amount, TargetCounted = target != null } by row.OccurredOn into bucket
                select new
                {
                    Day = bucket.Key,
                    Total = bucket.Sum(item =>
                        item.Kind == TransactionKind.Income ? item.Amount
                        : item.Kind == TransactionKind.Transfer && item.TargetCounted ? 0m
                        : -item.Amount)
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<DateOnly, Money> result = new(totals.Count);

        foreach (var total in totals)
        {
            result[total.Day] = Money.Restore(total.Total, Currency.RUB);
        }

        return result;
    }

    /// <summary>
    /// Итог дня ленты счёта — в его валюте, обе стороны переводов. Две сводки
    /// по двум индексам, слитые в памяти по дате: дней на странице десятки,
    /// а операций за день может быть сколько угодно.
    /// </summary>
    private static async Task<Dictionary<DateOnly, Money>> AccountDayTotalsAsync(
        FinanceDbContext context,
        Guid accountKey,
        DateOnly earliest,
        DateOnly latest,
        CancellationToken cancellationToken)
    {
        Currency currency = await context.Accounts
            .AsNoTracking()
            .Where(account => account.Key == accountKey)
            .Select(account => account.Currency)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<TransactionRow> inRange = context.Transactions
            .AsNoTracking()
            .Where(row => row.OccurredOn >= earliest && row.OccurredOn <= latest);

        var outgoing = await inRange
            .Where(row => row.SourceAccountKey == accountKey)
            .GroupBy(row => row.OccurredOn)
            .Select(group => new
            {
                Day = group.Key,
                Total = group.Sum(row => row.Kind == TransactionKind.Income ? row.Amount : -row.Amount)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var incoming = await inRange
            .Where(row => row.TargetAccountKey == accountKey)
            .GroupBy(row => row.OccurredOn)
            .Select(group => new
            {
                Day = group.Key,
                Total = group.Sum(row => row.TargetAmount!.Value)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<DateOnly, decimal> merged = [];

        foreach (var total in outgoing)
        {
            merged[total.Day] = merged.GetValueOrDefault(total.Day) + total.Total;
        }

        foreach (var total in incoming)
        {
            merged[total.Day] = merged.GetValueOrDefault(total.Day) + total.Total;
        }

        Dictionary<DateOnly, Money> result = new(merged.Count);

        foreach ((DateOnly day, decimal total) in merged)
        {
            result[day] = Money.Restore(total, currency);
        }

        return result;
    }

    /// <summary>
    /// Проекция с соединениями. Все, кроме счёта списания, — левые: у дохода нет второго
    /// счёта, у перевода нет категории, а место могло быть удалено из справочника,
    /// и такая операция показывается как операция без места.
    /// </summary>
    private static IQueryable<Projection> Project(FinanceDbContext context, IQueryable<TransactionRow> transactions) =>
        from row in transactions
        join source in context.Accounts.AsNoTracking() on row.SourceAccountKey equals source.Key
        join target in context.Accounts.AsNoTracking() on row.TargetAccountKey equals target.Key into targets
        from target in targets.DefaultIfEmpty()
        join category in context.Categories.AsNoTracking() on row.CategoryKey equals category.Key into categories
        from category in categories.DefaultIfEmpty()
        join parent in context.Categories.AsNoTracking() on category.ParentKey equals parent.Key into parents
        from parent in parents.DefaultIfEmpty()
        join place in context.Places.AsNoTracking() on row.PlaceKey equals place.Key into places
        from place in places.DefaultIfEmpty()
        select new Projection
        {
            Key = row.Key,
            Kind = row.Kind,
            OccurredOn = row.OccurredOn,
            CreatedAtUtc = row.CreatedAtUtc,
            Amount = row.Amount,
            TargetAmount = row.TargetAmount,
            Note = row.Note,
            SourceAccountKey = source.Key,
            SourceAccountName = source.Name,
            SourceCurrency = source.Currency,
            SourceType = source.Type,
            SourceExcluded = source.ExcludedFromTotals,
            SourceColor = source.Color,
            SourceIcon = source.Icon,
            TargetAccountKey = target != null ? target.Key : null,
            TargetAccountName = target != null ? target.Name : null,
            TargetCurrency = target != null ? target.Currency : null,
            TargetType = target != null ? target.Type : null,
            TargetExcluded = target != null ? target.ExcludedFromTotals : null,
            TargetColor = target != null ? target.Color : null,
            TargetIcon = target != null ? target.Icon : null,
            CategoryName = category != null ? category.Name : null,
            CategoryIcon = category != null ? category.Icon : null,
            GroupName = parent != null ? parent.Name : null,
            PlaceName = place != null ? place.Name : null
        };

    /// <summary>
    /// Что база отдаёт на строку до выбора стороны.
    /// </summary>
    internal sealed class Projection
    {
        public required Guid Key { get; init; }

        public required TransactionKind Kind { get; init; }

        public required DateOnly OccurredOn { get; init; }

        public required DateTimeOffset CreatedAtUtc { get; init; }

        public required decimal Amount { get; init; }

        public decimal? TargetAmount { get; init; }

        public string? Note { get; init; }

        public required Guid SourceAccountKey { get; init; }

        public required string SourceAccountName { get; init; }

        public required Currency SourceCurrency { get; init; }

        public required AccountType SourceType { get; init; }

        public required bool SourceExcluded { get; init; }

        public required AccountColor SourceColor { get; init; }

        public string? SourceIcon { get; init; }

        public Guid? TargetAccountKey { get; init; }

        public string? TargetAccountName { get; init; }

        public Currency? TargetCurrency { get; init; }

        public AccountType? TargetType { get; init; }

        public bool? TargetExcluded { get; init; }

        public AccountColor? TargetColor { get; init; }

        public string? TargetIcon { get; init; }

        /// <summary>
        /// Знак счёта списания.
        /// </summary>
        public AccountMark SourceMark =>
            new(SourceAccountName, SourceColor, AccountIcon.For(SourceType, SourceExcluded, SourceIcon));

        /// <summary>
        /// Знак счёта зачисления; у дохода и расхода пусто.
        /// </summary>
        public AccountMark? TargetMark => TargetAccountName is null
            ? null
            : new(TargetAccountName, TargetColor!.Value, AccountIcon.For(TargetType!.Value, TargetExcluded!.Value, TargetIcon));

        public string? CategoryName { get; init; }

        public string? CategoryIcon { get; init; }

        public string? GroupName { get; init; }

        public string? PlaceName { get; init; }
    }
}
