namespace Finance.Application.Features.Accounts.Card;

/// <summary>Заведение и правка счёта.</summary>
public interface ISaveAccountHandler
{
    /// <summary>
    /// Сохраняет счёт и возвращает его ключ.
    /// </summary>
    /// <param name="command">Что введено в карточке.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="Finance.Domain.DomainException">Нарушено доменное правило: имя занято, валюта заперта операциями, дата открытия позже операций.</exception>
    Task<Guid> HandleAsync(SaveAccountCommand command, CancellationToken cancellationToken = default);
}
