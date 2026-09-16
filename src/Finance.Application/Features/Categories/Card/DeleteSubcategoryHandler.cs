using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Удаляет подкатегорию, перенося её операции в приёмник группы. Обе правки идут
/// одной транзакцией: порознь операции остались бы на удалённой категории и
/// пропали бы из отчёта молча.
/// </summary>
public sealed class DeleteSubcategoryHandler : IDeleteSubcategoryHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: момент удаления.</param>
    public DeleteSubcategoryHandler(UnitOfWork unitOfWork, IClock clock)
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
                CategoryRow? row = await context.Categories
                    .FirstOrDefaultAsync(existing => existing.Key == key, token)
                    .ConfigureAwait(false);

                // Уже удалённая под фильтр не попадает: повторное удаление —
                // не ошибка, а нажатие по экрану, который не успел обновиться
                if (row is null)
                {
                    return DataChange.None;
                }

                Category deleted = row.ToDomain();

                // Сначала проверка, потом переезд: удаление само отвергает группу,
                // приёмник и служебную категорию, а лишний откат транзакции —
                // повод для вопросов там, где ответ известен заранее
                deleted.Delete(_clock.NowUtc);

                Category receiver = await ReceiverForAsync(context, deleted, token).ConfigureAwait(false);

                await MoveTransactionsAsync(context, key, receiver.Key, token).ConfigureAwait(false);

                deleted.CopyTo(row);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return DataChange.Categories | DataChange.Transactions;
            },
            cancellationToken);

    /// <summary>
    /// Приёмник выбирает домен: ему для этого нужны группа и все её подкатегории.
    /// </summary>
    private static async Task<Category> ReceiverForAsync(
        FinanceDbContext context,
        Category deleted,
        CancellationToken cancellationToken)
    {
        Guid parentKey = deleted.ParentKey!.Value;

        CategoryRow parentRow = await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Key == parentKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Группа {parentKey} удаляемой категории «{deleted.Name}» не найдена");

        List<CategoryRow> siblingRows = await context.Categories
            .AsNoTracking()
            .Where(row => row.ParentKey == parentKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Category parent = parentRow.ToDomain();
        Category[] siblings = [.. siblingRows.Select(static row => row.ToDomain())];

        return CategoryRules.ReceiverFor(parent, siblings, deleted);
    }

    /// <summary>
    /// Переставляет ссылки операций на приёмник.
    /// </summary>
    /// <remarks>
    /// Строки поднимаются в память, а не правятся через <c>ExecuteUpdate</c>, и это
    /// решение, а не небрежность: <c>ExecuteUpdate</c> идёт отдельным запросом мимо
    /// <c>SaveChanges</c>, а именно там проставляется <c>updated_at_utc</c> и будет
    /// наполняться очередь отправки. Переезд уехал бы мимо будущего обмена, и на
    /// втором устройстве операции навсегда остались бы на удалённой категории.
    /// Объём ограничен операциями одной подкатегории, выборка идёт по индексу.
    /// Мягко удалённые операции фильтр не пропускает — они остаются на удалённой
    /// категории, и это разрешено: ссылка на удалённую подкатегорию допустима.
    /// </remarks>
    private static async Task MoveTransactionsAsync(
        FinanceDbContext context,
        Guid from,
        Guid to,
        CancellationToken cancellationToken)
    {
        List<TransactionRow> affected = await context.Transactions
            .Where(row => row.CategoryKey == from)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (TransactionRow transaction in affected)
        {
            transaction.CategoryKey = to;
        }
    }
}
