using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Подкатегории стартового набора с тем, что о них нужно сверке с моделью: группа,
/// её вид, универсальность и признак «вне отчётов» на любом из двух уровней.
/// </summary>
internal sealed class CategoryCatalog
{
    private CategoryCatalog(IReadOnlyList<Entry> entries) => Entries = entries;

    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>
    /// Читает подкатегории из базы со стартовым набором. Ключи набора выводятся
    /// из имён, поэтому в каждой такой базе они одни и те же.
    /// </summary>
    public static async Task<CategoryCatalog> LoadAsync(TestDatabase database)
    {
        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        List<Entry> entries = await (
                from subcategory in context.Categories
                join parent in context.Categories on subcategory.ParentKey equals parent.Key
                orderby subcategory.Key
                select new Entry(
                    subcategory.Key,
                    parent.Key,
                    parent.Kind!.Value,
                    parent.AcceptsAnyKind == true,
                    subcategory.ExcludeFromReports || parent.ExcludeFromReports))
            .ToListAsync();

        return new CategoryCatalog(entries);
    }

    /// <summary>
    /// Подкатегории, в которые можно записать операцию этого вида: группы того же
    /// вида и универсальные — у них и возвраты.
    /// </summary>
    public IReadOnlyList<Entry> Accepting(TransactionKind kind) =>
        [.. Entries.Where(entry => entry.Accepts(kind))];

    public Entry this[Guid key] => Entries.Single(entry => entry.Key == key);

    /// <summary>
    /// Подкатегория глазами отчёта.
    /// </summary>
    internal sealed record Entry(Guid Key, Guid GroupKey, CategoryKind GroupKind, bool AcceptsAnyKind, bool ExcludedFromReports)
    {
        public bool Accepts(TransactionKind kind) =>
            AcceptsAnyKind || (kind is TransactionKind.Income) == (GroupKind is CategoryKind.Income);
    }
}
