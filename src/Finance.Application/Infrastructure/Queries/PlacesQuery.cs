using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Чтение справочника мест вместе со счётчиками использования.
/// </summary>
public sealed class PlacesQuery : IPlacesQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public PlacesQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaceListItem>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var places = await context.Places
            .AsNoTracking()
            .Select(row => new { row.Key, row.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (places.Count is 0)
        {
            return [];
        }

        // Счёт ведёт база: операций тысячи, а справочнику нужны от них два числа.
        // Группировка сразу по паре «место и подкатегория» даёт и общее число
        // (суммой по месту), и самую частую подкатегорию (наибольшей из групп) —
        // вторым запросом ради второго числа ходить незачем
        var usage = await context.Transactions
            .AsNoTracking()
            .Where(row => row.PlaceKey != null)
            .GroupBy(row => new { row.PlaceKey, row.CategoryKey })
            .Select(bucket => new
            {
                bucket.Key.PlaceKey,
                bucket.Key.CategoryKey,
                Count = bucket.Count()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, string> categoryNames = await context.Categories
            .AsNoTracking()
            .Where(row => row.ParentKey != null)
            .ToDictionaryAsync(row => row.Key, row => row.Name, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, (int Total, int TopCount, string? TopCategory)> counters = new(places.Count);

        foreach (var bucket in usage)
        {
            Guid placeKey = bucket.PlaceKey!.Value;

            counters.TryGetValue(placeKey, out (int Total, int TopCount, string? TopCategory) counter);

            // Подкатегория могла быть удалена вместе с переездом операций в приёмник,
            // но операция, удалённая мягко, остаётся на прежней: имени для неё нет,
            // и место просто покажется без подписи
            string? name = bucket.CategoryKey is { } categoryKey
                           && categoryNames.TryGetValue(categoryKey, out string? found)
                ? found
                : null;

            counters[placeKey] = bucket.Count > counter.TopCount && name is not null
                ? (counter.Total + bucket.Count, bucket.Count, name)
                : (counter.Total + bucket.Count, counter.TopCount, counter.TopCategory);
        }

        List<PlaceListItem> items = new(places.Count);

        foreach (var place in places)
        {
            counters.TryGetValue(place.Key, out (int Total, int TopCount, string? TopCategory) counter);

            items.Add(new PlaceListItem
            {
                Key = place.Key,
                Name = place.Name,
                TransactionCount = counter.Total,
                TopCategoryName = counter.TopCategory
            });
        }

        // Порядок выстраивается здесь, а не в базе: SQLite сравнивает строки по кодам
        // символов, а строчная «ё» стоит в кодировке после «я» — «ёлки» уехали бы
        // в конец справочника
        items.Sort(static (left, right) => left.TransactionCount == right.TransactionCount
            ? string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase)
            : right.TransactionCount - left.TransactionCount);

        return items;
    }
}
