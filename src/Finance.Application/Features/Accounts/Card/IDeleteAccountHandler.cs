namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Удаление счёта. Модель представления зовёт его напрямую.
/// </summary>
public interface IDeleteAccountHandler
{
    /// <summary>
    /// Удаляет счёт мягко, если по нему нет операций.
    /// </summary>
    /// <param name="key">Ключ счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="Finance.Domain.Errors.DomainException">По счёту есть операции.</exception>
    Task HandleAsync(Guid key, CancellationToken cancellationToken = default);
}
