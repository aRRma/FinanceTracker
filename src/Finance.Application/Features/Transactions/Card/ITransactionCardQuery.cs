namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Чтение операции для карточки правки.
/// </summary>
public interface ITransactionCardQuery
{
    /// <summary>
    /// Читает операцию по ключу.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Карточка или <c>null</c>, если операции нет или она удалена.</returns>
    Task<TransactionCard?> ReadAsync(Guid key, CancellationToken cancellationToken = default);
}
