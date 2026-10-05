namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Чтение списков выбора для формы операции.
/// </summary>
public interface ITransactionFormQuery
{
    /// <summary>
    /// Читает счета, подкатегории и счёт по умолчанию.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<TransactionForm> ReadAsync(CancellationToken cancellationToken = default);
}
