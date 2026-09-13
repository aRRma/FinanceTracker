using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>Чтение списка счетов с балансами.</summary>
public sealed class AccountsQuery : IAccountsQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>Создаёт запрос.</summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public AccountsQuery(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountListItem>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Проекция на стороне базы: доменный счёт здесь не нужен, а строка целиком
        // тянула бы за собой поля обмена, которых на экране нет
        var accounts = await context.Accounts
            .AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Name)
            .Select(row => new
            {
                row.Key,
                row.Name,
                row.Type,
                row.Currency,
                row.OpeningBalance,
                row.OpenedOn,
                row.ExcludedFromTotals,
                row.IsClosed,
                row.SortOrder
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> movements = await AccountBalances
            .ReadMovementsAsync(context, cancellationToken)
            .ConfigureAwait(false);

        List<AccountListItem> items = new(accounts.Count);

        foreach (var account in accounts)
        {
            decimal balance = account.OpeningBalance + movements.GetValueOrDefault(account.Key);

            items.Add(new AccountListItem
            {
                Key = account.Key,
                Name = account.Name,
                Type = account.Type,

                // Restore, а не Create: число уже прошло проверку при вводе,
                // а баланс ещё и не обязан укладываться в предел суммы операции
                Balance = Money.Restore(balance, account.Currency),
                OpeningBalance = Money.Restore(account.OpeningBalance, account.Currency),
                OpenedOn = account.OpenedOn,
                ExcludedFromTotals = account.ExcludedFromTotals,
                IsClosed = account.IsClosed,
                SortOrder = account.SortOrder
            });
        }

        return items;
    }
}
