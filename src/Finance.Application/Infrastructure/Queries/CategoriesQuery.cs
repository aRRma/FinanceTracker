using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Чтение справочника категорий целиком.
/// </summary>
public sealed class CategoriesQuery : ICategoriesQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public CategoriesQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryListItem>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Соединения с самой собой ради вида подкатегории нет намеренно: справочник
        // читается целиком и весь помещается в памяти, а порядок всё равно
        // выстраивается здесь — база не знает русского алфавита
        var rows = await context.Categories
            .AsNoTracking()
            .Select(row => new
            {
                row.Key,
                row.ParentKey,
                row.Kind,
                row.AcceptsAnyKind,
                row.Name,
                row.Icon,
                row.Role
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groups = rows
            .Where(row => row.ParentKey is null)
            .OrderBy(row => row.Role is CategoryRole.Service)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var subcategories = rows
            .Where(row => row.ParentKey is not null)
            .ToLookup(row => row.ParentKey!.Value);

        List<CategoryListItem> items = new(rows.Count);

        foreach (var parent in groups)
        {
            CategoryKind kind = parent.Kind
                                ?? throw new InvalidOperationException(Faults.GroupKindMissing(parent.Name));

            bool anyKind = parent.AcceptsAnyKind is true;

            items.Add(new CategoryListItem
            {
                Key = parent.Key,
                ParentKey = null,
                Kind = kind,
                AcceptsAnyKind = anyKind,
                Name = parent.Name,
                Icon = parent.Icon,
                Role = parent.Role
            });

            // «Прочее» и служебная уходят в конец группы: они не удаляются и не
            // переносятся, и в справочнике место им — под обычными подкатегориями.
            // Экран выбора ставит «Прочее» первым сам: там в него записывают
            IOrderedEnumerable<CategoryListItem> children = subcategories[parent.Key]
                .Select(row => new CategoryListItem
                {
                    Key = row.Key,
                    ParentKey = parent.Key,
                    Kind = kind,
                    AcceptsAnyKind = anyKind,
                    Name = row.Name,
                    Icon = row.Icon,
                    Role = row.Role
                })
                .OrderBy(static child => child.IsProtected)
                .ThenBy(static child => child.Name, StringComparer.CurrentCultureIgnoreCase);

            items.AddRange(children);
        }

        // Группы не удаляются вовсе, поэтому подкатегория без группы означает
        // испорченные данные. Молча потерять её нельзя: в справочнике её не будет,
        // а операции на ней останутся
        if (items.Count != rows.Count)
        {
            throw new InvalidOperationException(
                Faults.CategoriesLost(rows.Count, items.Count));
        }

        return items;
    }
}
