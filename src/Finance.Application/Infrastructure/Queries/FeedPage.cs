using Finance.Domain.Values;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Страница ленты: строки и итоги дней, в которые эти строки попали. Итоги считаются
/// отдельно от строк, потому что день может не уместиться в страницу целиком,
/// а итог в его шапке обязан быть за весь день.
/// </summary>
/// <param name="Items">Строки от новых к старым.</param>
/// <param name="DayTotals">Итог каждого дня, встретившегося в <paramref name="Items"/>.</param>
/// <param name="HasMore">За последней строкой есть ещё — можно спросить следующую страницу.</param>
public sealed record FeedPage(
    IReadOnlyList<FeedItem> Items,
    IReadOnlyDictionary<DateOnly, Money> DayTotals,
    bool HasMore)
{
    /// <summary>
    /// Пустая страница: операций нет.
    /// </summary>
    public static FeedPage Empty { get; } = new([], new Dictionary<DateOnly, Money>(), HasMore: false);
}
