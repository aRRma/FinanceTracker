using Finance.Domain.Errors;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Удаление подкатегории вместе с переездом её операций. Модель представления зовёт его напрямую.
/// </summary>
public interface IDeleteSubcategoryHandler
{
    /// <summary>
    /// Переносит операции подкатегории в приёмник её группы и помечает подкатегорию
    /// удалённой — одной транзакцией. Порознь эти два действия оставили бы операции
    /// висеть на удалённой категории.
    /// </summary>
    /// <param name="key">Ключ удаляемой подкатегории.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="DomainException">Категория защищена от удаления — какими правилами, сказано ниже.</exception>
    /// <remarks>
    /// Удаляют приёмник или служебную категорию (<see cref="Invariant.ProtectedCategoryStays"/>), группу
    /// (<see cref="Invariant.GroupNotDeleted"/>) или подкатегорию группы без приёмника (<see cref="Invariant.GroupHasReceiver"/>).
    /// </remarks>
    Task HandleAsync(Guid key, CancellationToken cancellationToken = default);
}
