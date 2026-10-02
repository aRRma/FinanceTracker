using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Deletion;

/// <summary>
/// Чтение последствий удаления операций для диалога подтверждения.
/// </summary>
public sealed class TransactionDeletionQuery : ITransactionDeletionQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly IAccountsQuery _accounts;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="accounts">Счета с балансами.</param>
    public TransactionDeletionQuery(IDbContextFactory<FinanceDbContext> contexts, IAccountsQuery accounts)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(accounts);

        _contexts = contexts;
        _accounts = accounts;
    }

    /// <inheritdoc />
    public async Task<TransactionDeletion> ReadAsync(
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        List<Sides> sides = await ReadSidesAsync(keys, cancellationToken).ConfigureAwait(false);

        // Сдвиг баланса каждого счёта; порядок — в каком счета встретились,
        // чтобы у перевода счёт списания называться первым
        List<Guid> order = [];
        Dictionary<Guid, decimal> shifts = [];

        foreach (Sides side in sides)
        {
            // Удалённый расход и списание перевода возвращают деньги на счёт,
            // удалённый доход их забирает
            Shift(side.SourceAccountKey, side.Kind is TransactionKind.Income ? -side.Amount : side.Amount);

            if (side.TargetAccountKey is { } target && side.TargetAmount is { } targetAmount)
            {
                Shift(target, -targetAmount);
            }
        }

        List<BalanceAfterDeletion> balances = [];

        // Счетов у выделенных операций — единицы, а полный список посчитал бы
        // балансы всех счетов по всей истории
        foreach (Guid key in order)
        {
            // Расход и доход на ту же сумму друг друга гасят: «станет» с прежним
            // балансом обещало бы перемену, которой не будет
            if (shifts[key] == 0m)
            {
                continue;
            }

            AccountListItem? account = await _accounts.ReadOneAsync(key, cancellationToken).ConfigureAwait(false);

            if (account is not null)
            {
                Money after = Money.Restore(account.Balance.Amount + shifts[key], account.Balance.Currency);

                balances.Add(new BalanceAfterDeletion(account.Name, after));
            }
        }

        return new TransactionDeletion { Count = sides.Count, Balances = balances };

        void Shift(Guid account, decimal amount)
        {
            if (shifts.TryGetValue(account, out decimal known))
            {
                shifts[account] = known + amount;
            }
            else
            {
                order.Add(account);
                shifts[account] = amount;
            }
        }
    }

    /// <summary>
    /// Стороны операций в порядке ленты — сверху вниз, как их видит пользователь:
    /// в этом порядке счета и называются в диалоге. Суммы — из записанного, а не
    /// с экрана: форму могли уже поправить, а удаляется операция как записана.
    /// </summary>
    private async Task<List<Sides>> ReadSidesAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
    {
        await using FinanceDbContext context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Transactions
            .AsNoTracking()
            .Where(row => keys.Contains(row.Key))
            .OrderByDescending(static row => row.OccurredOn)
            .ThenByDescending(static row => row.CreatedAtUtc)
            .Select(static row => new Sides(row.Kind, row.SourceAccountKey, row.Amount, row.TargetAccountKey, row.TargetAmount))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Стороны операции — всё, что нужно, чтобы посчитать балансы после удаления.
    /// </summary>
    private sealed record Sides(
        TransactionKind Kind,
        Guid SourceAccountKey,
        decimal Amount,
        Guid? TargetAccountKey,
        decimal? TargetAmount);
}
