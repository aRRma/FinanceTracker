using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>Чтение операции для карточки правки.</summary>
public sealed class TransactionCardQuery : ITransactionCardQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>Создаёт запрос.</summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public TransactionCardQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<TransactionCard?> ReadAsync(Guid key, CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Место — левым соединением: удалённое из справочника отфильтровано,
        // и операция показывается как операция без места
        return await (
            from row in context.Transactions.AsNoTracking()
            where row.Key == key
            join place in context.Places.AsNoTracking() on row.PlaceKey equals place.Key into places
            from place in places.DefaultIfEmpty()
            select new TransactionCard
            {
                Key = row.Key,
                Kind = row.Kind,
                SourceAccountKey = row.SourceAccountKey,
                TargetAccountKey = row.TargetAccountKey,
                Amount = row.Amount,
                TargetAmount = row.TargetAmount,
                CategoryKey = row.CategoryKey,
                PlaceName = place != null ? place.Name : null,
                OccurredOn = row.OccurredOn,
                Note = row.Note
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
