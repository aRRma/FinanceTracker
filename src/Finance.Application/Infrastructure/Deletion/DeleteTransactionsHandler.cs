using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Deletion;

/// <summary>
/// Помечает операции удалёнными. Физического удаления нет: надгробие — единственный
/// способ, которым будущий обмен узнает об удалении, иначе запись воскреснет
/// с другого устройства.
/// </summary>
/// <remarks>
/// Общий для формы операции и ленты: одна операция — список из одной. Несколько
/// операций удаляются одной транзакцией и одним оповещением — иначе лента
/// перечитывалась бы на каждую строку и показывала удаление наполовину.
/// </remarks>
public sealed class DeleteTransactionsHandler : IDeleteTransactionsHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: момент удаления.</param>
    public DeleteTransactionsHandler(UnitOfWork unitOfWork, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task HandleAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return _unitOfWork.ExecuteAsync(
            async (context, token) =>
            {
                // Уже удалённые под фильтр не попадают: повторное удаление —
                // не ошибка, а нажатие по экрану, который не успел обновиться
                List<TransactionRow> rows = await context.Transactions
                    .Where(row => keys.Contains(row.Key))
                    .ToListAsync(token)
                    .ConfigureAwait(false);

                if (rows.Count == 0)
                {
                    return DataChange.None;
                }

                Dictionary<Guid, Currency> currencies = await CurrenciesAsync(context, rows, token).ConfigureAwait(false);
                DateTimeOffset now = _clock.NowUtc;

                foreach (TransactionRow row in rows)
                {
                    // Доменная операция собирается ради правила мягкого удаления:
                    // пометку ставит она, а не строка
                    Transaction transaction = row.ToDomain(
                        currencies[row.SourceAccountKey],
                        row.TargetAccountKey is { } target ? currencies[target] : null);

                    transaction.Delete(now);
                    transaction.CopyTo(row);
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return DataChange.Transactions;
            },
            cancellationToken);
    }

    /// <summary>
    /// Валюты счетов всех операций одним запросом: строке их не хватает, чтобы стать
    /// доменной операцией, а читать счета на каждую строку значило бы ходить в базу
    /// столько раз, сколько строк выделено.
    /// </summary>
    private static async Task<Dictionary<Guid, Currency>> CurrenciesAsync(
        FinanceDbContext context,
        List<TransactionRow> rows,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> keys = [];

        foreach (TransactionRow row in rows)
        {
            keys.Add(row.SourceAccountKey);

            if (row.TargetAccountKey is { } target)
            {
                keys.Add(target);
            }
        }

        Dictionary<Guid, Currency> currencies = await context.Accounts
            .AsNoTracking()
            .Where(account => keys.Contains(account.Key))
            .Select(static account => new { account.Key, account.Currency })
            .ToDictionaryAsync(static account => account.Key, static account => account.Currency, cancellationToken)
            .ConfigureAwait(false);

        foreach (Guid key in keys)
        {
            if (!currencies.ContainsKey(key))
            {
                throw new InvalidOperationException(Faults.AccountNotFound(key));
            }
        }

        return currencies;
    }
}
