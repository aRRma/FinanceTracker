namespace Finance.Application.Infrastructure.Deletion;

/// <summary>
/// Что сказать в диалоге перед удалением операций.
/// </summary>
public interface ITransactionDeletionQuery
{
    /// <summary>
    /// Читает последствия удаления: сколько операций уйдёт и какими станут балансы
    /// их счетов. Уже удалённые не считаются.
    /// </summary>
    /// <param name="keys">Ключи операций.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<TransactionDeletion> ReadAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default);
}
