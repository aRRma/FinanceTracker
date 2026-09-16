using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Чтение счёта для карточки правки.
/// </summary>
public sealed class AccountCardQuery : IAccountCardQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
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

        // Движения читаются по всем счетам разом: отдельного запроса на один счёт
        // нет, а сводка по всем — два запроса по индексам, не полный проход
        Dictionary<Guid, decimal> movements = await AccountBalances
            .ReadMovementsAsync(context, cancellationToken)
            .ConfigureAwait(false);

        Money balance = Money.Restore(
            account.OpeningBalance + movements.GetValueOrDefault(key),
            account.Currency);

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
            Balance = balance,
            CurrencyLocked = hasEverHadTransactions,
            EarliestTransactionOn = earliest
        };
    }
}
