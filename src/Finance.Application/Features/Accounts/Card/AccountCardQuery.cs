using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>Чтение счёта для карточки правки.</summary>
public sealed class AccountCardQuery : IAccountCardQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>Создаёт запрос.</summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public AccountCardQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<AccountCard?> ReadAsync(Guid key, CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var account = await context.Accounts
            .AsNoTracking()
            .Where(row => row.Key == key)
            .Select(row => new
            {
                row.Key,
                row.Name,
                row.Type,
                row.Currency,
                row.OpeningBalance,
                row.OpenedOn,
                row.ExcludedFromTotals,
                row.IsClosed
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        bool hasEverHadTransactions = await AccountTransactions
            .ExistedEverAsync(context, key, cancellationToken)
            .ConfigureAwait(false);

        DateOnly? earliest = await AccountTransactions
            .EarliestOnAsync(context, key, cancellationToken)
            .ConfigureAwait(false);

        return new AccountCard
        {
            Key = account.Key,
            Name = account.Name,
            Type = account.Type,
            Currency = account.Currency,
            OpeningBalance = account.OpeningBalance,
            OpenedOn = account.OpenedOn,
            ExcludedFromTotals = account.ExcludedFromTotals,
            IsClosed = account.IsClosed,
            CurrencyLocked = hasEverHadTransactions,
            EarliestTransactionOn = earliest
        };
    }
}
