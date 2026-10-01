namespace Finance.Application.Infrastructure.Deletion;

/// <summary>
/// Удаление операций — одной или нескольких сразу. Подтверждение — забота экрана:
/// обработчик уже не спрашивает.
/// </summary>
public interface IDeleteTransactionsHandler
{
    /// <summary>
    /// Помечает операции удалёнными одной транзакцией. Уже удалённые пропускаются.
    /// </summary>
    /// <param name="keys">Ключи операций.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task HandleAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default);
}
