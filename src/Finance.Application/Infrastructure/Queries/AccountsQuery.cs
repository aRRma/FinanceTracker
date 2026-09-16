using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
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

        List<Row> accounts = await Project(context.Accounts.AsNoTracking()
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> movements = await AccountBalances
            .ReadMovementsAsync(context, cancellationToken)
            .ConfigureAwait(false);

        List<AccountListItem> items = new(accounts.Count);

        foreach (Row account in accounts)
        {
            items.Add(ToItem(account, movements.GetValueOrDefault(account.Key)));
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<AccountListItem?> ReadOneAsync(Guid key, CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Row? account = await Project(context.Accounts.AsNoTracking().Where(row => row.Key == key))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        decimal movement = await AccountBalances
            .ReadMovementAsync(context, key, cancellationToken)
            .ConfigureAwait(false);

        return ToItem(account, movement);
    }

    // Проекция на стороне базы: доменный счёт здесь не нужен, а строка целиком
    // тянула бы за собой поля обмена, которых на экране нет
    private static IQueryable<Row> Project(IQueryable<AccountRow> accounts) =>
        accounts.Select(row => new Row(
            row.Key,
            row.Name,
            row.Type,
            row.Currency,
            row.OpeningBalance,
            row.OpenedOn,
            row.ExcludedFromTotals,
            row.IsClosed,
            row.SortOrder));

    private static AccountListItem ToItem(Row account, decimal movement) =>
        new()
        {
            Key = account.Key,
            Name = account.Name,
            Type = account.Type,

            // Restore, а не Create: число уже прошло проверку при вводе,
            // а баланс ещё и не обязан укладываться в предел суммы операции
            Balance = Money.Restore(account.OpeningBalance + movement, account.Currency),
            OpeningBalance = Money.Restore(account.OpeningBalance, account.Currency),
            OpenedOn = account.OpenedOn,
            ExcludedFromTotals = account.ExcludedFromTotals,
            IsClosed = account.IsClosed,
            SortOrder = account.SortOrder
        };

    // Плоская строка чтения: одна проекция на оба запроса, полный список и один счёт
    private sealed record Row(
        Guid Key,
        string Name,
        AccountType Type,
        Currency Currency,
        decimal OpeningBalance,
        DateOnly OpenedOn,
        bool ExcludedFromTotals,
        bool IsClosed,
        int SortOrder);
}
