using System.Collections.ObjectModel;
using Finance.Application.Infrastructure;
using Finance.Domain.Values;

namespace Finance.Application.Features.Feed;

/// <summary>
/// День ленты: шапка с датой и итогом, под ней строки. Наследует коллекцию,
/// а не содержит её, — так группу понимает список с группировкой, а строки,
/// дочитанные следующей страницей, встают в уже показанный день.
/// </summary>
public sealed class FeedDay : ObservableCollection<FeedRowItem>
{
    /// <summary>
    /// Создаёт день ленты.
    /// </summary>
    /// <param name="date">Дата дня.</param>
    /// <param name="total">Итог дня; пусто, если в итог не попала ни одна строка.</param>
    /// <param name="today">Сегодняшняя дата пользователя — от неё зависит, писать ли год.</param>
    public FeedDay(DateOnly date, Money? total, DateOnly today)
    {
        Date = date;
        Title = Format(date, today);
        Total = total?.DisplaySigned ?? string.Empty;
        IsTotalPositive = total is { IsPositive: true };
    }

    /// <summary>
    /// Дата дня.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Шапка: «25 августа», с годом — если день не в этом году.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Итог дня со знаком. Пусто — итога нет: ни одна строка дня в него не входит.
    /// </summary>
    public string Total { get; }

    /// <summary>
    /// Итог положителен: день в плюсе показывается смысловым цветом, как доход в строке.
    /// </summary>
    public bool IsTotalPositive { get; }

    /// <summary>
    /// День показывает то же, что и другой: ту же шапку и те же строки в том же порядке.
    /// По этому признаку перечитанная лента меняет в списке только изменившиеся дни.
    /// </summary>
    /// <param name="other">Другой день.</param>
    public bool SameAs(FeedDay other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Date == other.Date
            && Title == other.Title
            && Total == other.Total
            && IsTotalPositive == other.IsTotalPositive
            && this.SequenceEqual(other);
    }

    /// <summary>
    /// Год пишется только чужой: в ленте за этот год он был бы шумом в каждой шапке.
    /// </summary>
    private static string Format(DateOnly date, DateOnly today) =>
        DateText.DayWithYearIfOther(date, today);
}
