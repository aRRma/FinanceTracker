namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Полный список категорий обоих уровней. Модель представления зовёт его напрямую.
/// </summary>
public interface ICategoriesQuery
{
    /// <summary>
    /// Читает все неудалённые категории в порядке показа: группы по алфавиту,
    /// служебные в конец, и сразу за каждой группой — её подкатегории тем же
    /// порядком, с «Прочее» и служебными в конце. Порядок задан здесь, а не на
    /// экранах: их три, и сортировка в каждом разошлась бы.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<CategoryListItem>> ReadAsync(CancellationToken cancellationToken = default);
}
