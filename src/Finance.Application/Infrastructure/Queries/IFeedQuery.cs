namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Лента операций — общая и по одному счёту. В инфраструктуре, а не в слайсе:
/// её читают вкладка операций и лента счёта. Третий уровень отчёта ленту
/// не читает: в отчёт валютные операции не входят вовсе, а в ленте они видны.
/// </summary>
public interface IFeedQuery
{
    /// <summary>
    /// Читает страницу ленты от новых к старым. Общая лента показывает перевод
    /// одной строкой со стороны счёта списания; лента счёта — со своей стороны,
    /// какой бы она ни была.
    /// </summary>
    /// <param name="accountKey">Счёт, чью ленту читать; пусто — общая лента.</param>
    /// <param name="skip">Сколько строк пропустить — столько уже показано.</param>
    /// <param name="take">Сколько строк прочитать.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<FeedPage> ReadAsync(Guid? accountKey, int skip, int take, CancellationToken cancellationToken = default);
}
