namespace Finance.Application.Features.Places.Card;

/// <summary>Переименование места. Модель представления зовёт его напрямую.</summary>
public interface IRenamePlaceHandler
{
    /// <summary>
    /// Переименовывает место. Операции не правятся: они ссылаются на ключ,
    /// и новое название видно в них сразу.
    /// </summary>
    /// <param name="key">Ключ места.</param>
    /// <param name="name">Новое название.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task HandleAsync(Guid key, string name, CancellationToken cancellationToken = default);
}
