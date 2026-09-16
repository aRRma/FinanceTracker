namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Удаление операции. Подтверждение — забота экрана: обработчик уже не спрашивает.
/// </summary>
public interface IDeleteTransactionHandler
{
    /// <summary>
    /// Помечает операцию удалённой. Повторное удаление ничего не меняет.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task HandleAsync(Guid key, CancellationToken cancellationToken = default);
}
