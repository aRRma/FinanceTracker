using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Places.Card;

/// <summary>
/// Переименовывает место. Ради этого справочник и заведён вместо свободной строки:
/// одна правка приводит в порядок всю историю, а не одну операцию.
/// </summary>
public sealed class RenamePlaceHandler : IRenamePlaceHandler
{
    private readonly UnitOfWork _unitOfWork;

    /// <summary>Создаёт обработчик.</summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    public RenamePlaceHandler(UnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public Task HandleAsync(Guid key, string name, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(
            async (context, token) =>
            {
                PlaceRow row = await context.Places
                    .FirstOrDefaultAsync(existing => existing.Key == key, token)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Место {key} не найдено");

                // Область поиска — весь справочник: у места нет ни вида, ни группы,
                // внутри которых имя могло бы повториться
                var neighbours = await context.Places
                    .AsNoTracking()
                    .Select(place => new { place.Key, place.Name })
                    .ToListAsync(token)
                    .ConfigureAwait(false);

                NameUniqueness.EnsureForRename(
                    name,
                    neighbours.Select(static place => (place.Key, place.Name)),
                    key,
                    "место");

                Place place = row.ToDomain();

                place.Rename(name);
                place.CopyTo(row);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                // Операции не менялись, но их показ — менялся: лента и карточка
                // берут название места соединением, и обе обязаны перечитаться
                return DataChange.Places | DataChange.Transactions;
            },
            cancellationToken);
}
