using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Places.Card;

/// <summary>
/// Удаляет место мягко и на этом останавливается. Переезда операций, как у
/// подкатегории, здесь нет намеренно: место необязательно, и операция без него —
/// законное состояние, а не потеря данных.
/// </summary>
public sealed class DeletePlaceHandler : IDeletePlaceHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Создаёт обработчик.</summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: момент удаления.</param>
    public DeletePlaceHandler(UnitOfWork unitOfWork, IClock clock)
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
                PlaceRow? row = await context.Places
                    .FirstOrDefaultAsync(existing => existing.Key == key, token)
                    .ConfigureAwait(false);

                // Уже удалённое под фильтр не попадает: повторное удаление —
                // не ошибка, а нажатие по экрану, который не успел обновиться
                if (row is null)
                {
                    return DataChange.None;
                }

                Place place = row.ToDomain();

                place.Delete(_clock.NowUtc);
                place.CopyTo(row);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                // Ссылки операций остаются нетронутыми, но разрешаться перестают:
                // лента и карточка операции покажут их без места и обязаны перечитаться
                return DataChange.Places | DataChange.Transactions;
            },
            cancellationToken);
}
