namespace Finance.Application.Features.Settings.DefaultAccounts;

/// <summary>
/// Выбор счёта по умолчанию. Модель представления зовёт его напрямую.
/// </summary>
public interface IChangeDefaultAccountHandler
{
    /// <summary>
    /// Запоминает выбор пользователя: с этой минуты форма новой операции подставляет этот счёт.
    /// </summary>
    /// <param name="accountKey">Ключ выбранного счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>false</c>, если счёт тем временем заблокирован или удалён и выбор не записан.</returns>
    Task<bool> HandleAsync(Guid accountKey, CancellationToken cancellationToken = default);
}
