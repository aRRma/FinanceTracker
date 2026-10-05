using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Чтение счёта для карточки правки.
/// </summary>
public interface IAccountCardQuery
{
    /// <summary>
    /// Читает счёт вместе с запретами, которые экран показывает сразу.
    /// </summary>
    /// <param name="key">Ключ счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Карточка счёта или <c>null</c>, если счёта нет.</returns>
    Task<AccountCard?> ReadAsync(Guid key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Читает цвет, который достанется новому счёту: карточка показывает его сразу,
    /// а пользователь вправе сменить до сохранения.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Первый по очереди цвет среди самых редких у незаблокированных счетов.</returns>
    Task<AccountColor> ReadFreeColorAsync(CancellationToken cancellationToken = default);
}
