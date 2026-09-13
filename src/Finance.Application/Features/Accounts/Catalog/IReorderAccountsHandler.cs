namespace Finance.Application.Features.Accounts.Catalog;

/// <summary>Перестановка счетов в списке перетаскиванием.</summary>
public interface IReorderAccountsHandler
{
    /// <summary>
    /// Задаёт порядок счетов. Принимается весь список целиком, а не пара
    /// «что и куда»: после перетаскивания пользователь видит именно этот порядок,
    /// и сохранять надо его, а не своё представление о том, что сдвинулось.
    /// </summary>
    /// <param name="keys">Ключи счетов в том порядке, в каком они теперь на экране.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task HandleAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken = default);
}
