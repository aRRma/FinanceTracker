using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Удаляет счёт, заведённый по ошибке. Есть ли по нему операции — с обеих сторон,
/// вместе с переводами на него, — читается здесь, а решает домен.
/// </summary>
public sealed class DeleteAccountHandler : IDeleteAccountHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: момент удаления.</param>
    public DeleteAccountHandler(UnitOfWork unitOfWork, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task HandleAsync(Guid key, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(
            async (context, token) =>
            {
                AccountRow? row = await context.Accounts
                    .FirstOrDefaultAsync(existing => existing.Key == key, token)
                    .ConfigureAwait(false);

                // Уже удалённое под фильтр не попадает: повторное удаление —
                // не ошибка, а нажатие по экрану, который не успел обновиться
                if (row is null)
                {
                    return DataChange.None;
                }

                bool hasTransactions = await AccountTransactions
                    .ExistsAsync(context, key, token)
                    .ConfigureAwait(false);

                Account account = row.ToDomain();

                account.Delete(_clock.NowUtc, hasTransactions);
                account.CopyTo(row);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return DataChange.Accounts;
            },
            cancellationToken);
}
