namespace Finance.Application.Features.Places.Card;

/// <summary>Удаление места. Модель представления зовёт его напрямую.</summary>
public interface IDeletePlaceHandler
{
    /// <summary>
    /// Удаляет место мягко. Строки операций не трогаются: они остаются такими,
    /// какими их ввели, и показываются как операции без места.
    /// </summary>
    /// <param name="key">Ключ места.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task HandleAsync(Guid key, CancellationToken cancellationToken = default);
}
