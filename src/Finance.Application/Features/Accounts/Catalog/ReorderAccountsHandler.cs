using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Accounts.Catalog;

/// <summary>Сохраняет порядок счетов, заданный перетаскиванием.</summary>
public sealed class ReorderAccountsHandler : IReorderAccountsHandler
{
    private readonly UnitOfWork _unitOfWork;

    /// <summary>Создаёт обработчик.</summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    public ReorderAccountsHandler(UnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public Task HandleAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return _unitOfWork.ExecuteAsync(
            async (context, token) =>
            {
                Dictionary<Guid, int> positions = [];

                for (int position = 0; position < keys.Count; position++)
                {
                    positions[keys[position]] = position;
                }

                List<AccountRow> rows = await context.Accounts
                    .Where(row => keys.Contains(row.Key))
                    .ToListAsync(token)
                    .ConfigureAwait(false);

                foreach (AccountRow row in rows)
                {
                    // Позиция в переданном списке и есть новый порядок.
                    // Счёт, которого в списке нет, не трогается: экран мог показывать
                    // не все счета, и обнулять порядок остальным нечем
                    Account account = row.ToDomain();
                    account.SetSortOrder(positions[row.Key]);
                    account.CopyTo(row);
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return DataChange.Accounts;
            },
            cancellationToken);
    }
}
