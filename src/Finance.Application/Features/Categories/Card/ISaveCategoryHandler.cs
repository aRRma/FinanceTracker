using Finance.Domain.Errors;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Запись категории. Модель представления зовёт его напрямую.
/// </summary>
public interface ISaveCategoryHandler
{
    /// <summary>
    /// Записывает категорию и возвращает её ключ. Заведение группы создаёт заодно
    /// приёмник «Прочее» — в той же транзакции, потому что группа без приёмника
    /// не примет операции удаляемых подкатегорий.
    /// </summary>
    /// <param name="command">Что записать.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="DomainException">
    /// Имя занято (<see cref="Invariant.NameUnique"/>), имя пусто
    /// (<see cref="Invariant.NameTrimmedAndNotEmpty"/>), перенос в группу другого вида
    /// или не принимающую вид записанных операций (<see cref="Invariant.MoveKeepsKind"/>),
    /// в служебную
    /// (<see cref="Invariant.ServiceGroupClosedToMoves"/>), перенос приёмника или
    /// служебной категории (<see cref="Invariant.ProtectedCategoryStays"/>),
    /// подкатегория в служебной группе (<see cref="Invariant.GroupHasReceiver"/>).
    /// </exception>
    Task<Guid> HandleAsync(SaveCategoryCommand command, CancellationToken cancellationToken = default);
}
