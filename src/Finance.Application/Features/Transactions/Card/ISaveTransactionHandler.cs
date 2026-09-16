namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Запись и правка операции.
/// </summary>
public interface ISaveTransactionHandler
{
    /// <summary>
    /// Сохраняет операцию и возвращает её ключ.
    /// </summary>
    /// <param name="command">Что введено в форме.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="Finance.Domain.Errors.DomainException">Нарушено доменное правило: закрытый счёт, дата раньше открытия, категория не того вида и подобное.</exception>
    Task<Guid> HandleAsync(SaveTransactionCommand command, CancellationToken cancellationToken = default);
}
