using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Частые подкатегории считает база: операций тысячи, а панели над списком нужны
/// от них восемь названий. Окно — последние месяцы, а не вся история: панель
/// затем и нужна, чтобы попасть в то, чем пользуются сейчас, а история помнит
/// и позапрошлогодние привычки.
/// </summary>
public sealed class FrequentCategoriesQuery : IFrequentCategoriesQuery
{
    /// <summary>
    /// Глубина окна частоты в месяцах.
    /// </summary>
    /// <remarks>
    /// Три месяца — сезон: переезд, отпуск или новая привычка попадают в панель
    /// за считаные недели, а редкая покупка раз в полгода её не занимает.
    /// </remarks>
    private const int WindowMonths = 3;

    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="clock">Часы: окно отсчитывается от сегодняшней даты пользователя.</param>
    public FrequentCategoriesQuery(IDbContextFactory<FinanceDbContext> contexts, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(clock);

        _contexts = contexts;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FrequentCategory>> ReadAsync(
        CategoryKind kind,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // «Сегодня» берётся у часов приложения, а не из UTC: окно отсчитывается
        // от даты пользователя, как и всё остальное в этом приложении
        return await Frequent(context, kind, _clock.Today.AddMonths(-WindowMonths), limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Запрос отдельно от выполнения: по составу индекса не видно, обслужит ли он
    /// группировку, — это видно только плану, а план снимается с готового SQL.
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="kind">Вид категорий.</param>
    /// <param name="since">Начало окна частоты.</param>
    /// <param name="limit">Сколько подкатегорий вернуть.</param>
    internal static IQueryable<FrequentCategory> Frequent(
        FinanceDbContext context,
        CategoryKind kind,
        DateOnly since,
        int limit)
    {
        // Вид операции сравнивается со значением, посчитанным заранее: в дереве
        // выражений сопоставление с образцом не живёт, а LINQ-запрос — дерево
        TransactionKind operationKind = kind is CategoryKind.Income
            ? TransactionKind.Income
            : TransactionKind.Expense;

        return (from row in context.Transactions.AsNoTracking()
         // Вид операции задан явно: у универсальной группы лежат оба вида,
         // и без этого условия расходные траты попадали бы в панель дохода
         where row.CategoryKey != null && row.OccurredOn >= since && row.Kind == operationKind
         join category in context.Categories.AsNoTracking() on row.CategoryKey equals category.Key
         // Вид хранится у группы, и без соединения со вторым уровнем расходные
         // подкатегории от доходных не отличить
         join parent in context.Categories.AsNoTracking() on category.ParentKey equals parent.Key
         // Служебная подкатегория в панели не нужна: ею правят расхождения,
         // а не записывают траты, и место частой она занимала бы зря
         where (parent.Kind == kind || parent.AcceptsAnyKind == true)
               && category.Role != CategoryRole.Service
         group row by new { category.Key, category.Name, category.Icon } into bucket
         // Ничья разрывается названием только ради устойчивого порядка: русского
         // алфавита SQLite не знает, но два запуска подряд обязаны дать одно и то же
         orderby bucket.Count() descending, bucket.Key.Name
         select new FrequentCategory
         {
             Key = bucket.Key.Key,
             Name = bucket.Key.Name,
             Icon = bucket.Key.Icon,
             Count = bucket.Count()
         })
            .Take(limit);
    }
}
