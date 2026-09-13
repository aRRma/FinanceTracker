using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Помечает операцию удалённой. Физического удаления нет: надгробие — единственный
/// способ, которым будущий обмен узнает об удалении, иначе запись воскреснет
/// с другого устройства.
/// </summary>
public sealed class DeleteTransactionHandler : IDeleteTransactionHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Создаёт обработчик.</summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: момент удаления.</param>
    public DeleteTransactionHandler(UnitOfWork unitOfWork, IClock clock)
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
                TransactionRow? row = await context.Transactions
                    .FirstOrDefaultAsync(existing => existing.Key == key, token)
                    .ConfigureAwait(false);

                // Уже удалённая под фильтр не попадает: повторное удаление —
                // не ошибка, а нажатие по экрану, который не успел обновиться
                if (row is null)
                {
                    return DataChange.None;
                }

                Transaction transaction = await TransactionSides
                    .ToDomainAsync(context, row, token)
                    .ConfigureAwait(false);

                transaction.Delete(_clock.NowUtc);
                transaction.CopyTo(row);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return DataChange.Transactions;
            },
            cancellationToken);
}
