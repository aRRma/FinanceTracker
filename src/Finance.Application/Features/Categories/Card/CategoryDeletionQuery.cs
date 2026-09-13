using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Categories.Card;

/// <summary>Чтение последствий удаления подкатегории.</summary>
public sealed class CategoryDeletionQuery : ICategoryDeletionQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>Создаёт запрос.</summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public CategoryDeletionQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<CategoryDeletion?> ReadAsync(Guid key, CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var subcategory = await context.Categories
            .AsNoTracking()
            .Where(row => row.Key == key)
            .Select(row => new { row.Name, row.ParentKey, row.Role })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (subcategory is null
            || subcategory.ParentKey is not { } parentKey
            || subcategory.Role is not CategoryRole.Normal)
        {
            return null;
        }

        var group = await context.Categories
            .AsNoTracking()
            .Where(row => row.Key == parentKey)
            .Select(row => new { row.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string? receiver = await context.Categories
            .AsNoTracking()
            .Where(row => row.ParentKey == parentKey && row.Role == CategoryRole.Other)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (group is null || receiver is null)
        {
            return null;
        }

        // Считает база: поднимать операции ради их числа значит поднять
        // всю ленту категории на экран, который показывает одну строку
        int count = await context.Transactions
            .AsNoTracking()
            .CountAsync(row => row.CategoryKey == key, cancellationToken)
            .ConfigureAwait(false);

        return new CategoryDeletion
        {
            Key = key,
            Name = subcategory.Name,
            GroupName = group.Name,
            ReceiverName = receiver,
            TransactionCount = count
        };
    }
}
