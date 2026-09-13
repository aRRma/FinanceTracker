namespace Finance.Application.Infrastructure.Queries;

/// <summary>Список счетов с балансами. Модель представления зовёт его напрямую.</summary>
public interface IAccountsQuery
{
    /// <summary>
    /// Читает все неудалённые счета в порядке, заданном пользователем, вместе
    /// с балансами. Закрытые тоже возвращаются: справочник показывает их
    /// отдельным разделом, а главный экран отбирает сам.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<AccountListItem>> ReadAsync(CancellationToken cancellationToken = default);
}
