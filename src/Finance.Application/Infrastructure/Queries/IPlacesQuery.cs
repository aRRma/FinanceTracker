namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Полный справочник мест со счётчиками. Модель представления зовёт его напрямую.
/// </summary>
public interface IPlacesQuery
{
    /// <summary>
    /// Читает все неудалённые места в порядке показа: сначала частые, при равном
    /// числе операций — по алфавиту. Порядок задан частотой, а не алфавитом:
    /// справочник наполняется сам и разрастается, а ищут в нём обычно то,
    /// чем пользуются.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<PlaceListItem>> ReadAsync(CancellationToken cancellationToken = default);
}
