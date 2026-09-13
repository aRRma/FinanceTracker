using System.Collections.ObjectModel;
using System.Globalization;
using Finance.Application.Infrastructure;
using Finance.Domain;

namespace Finance.Application.Features.Feed;

/// <summary>
/// День ленты: шапка с датой и итогом, под ней строки. Наследует коллекцию,
/// а не содержит её, — так группу понимает список с группировкой, а строки,
/// дочитанные следующей страницей, встают в уже показанный день.
/// </summary>
public sealed class FeedDay : ObservableCollection<FeedRowItem>
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Создаёт день ленты.</summary>
    /// <param name="date">Дата дня.</param>
    /// <param name="total">Итог дня; пусто, если в итог не попала ни одна строка.</param>
    /// <param name="today">Сегодняшняя дата пользователя — от неё зависит, писать ли год.</param>
    public FeedDay(DateOnly date, Money? total, DateOnly today)
    {
        Date = date;
        Title = Format(date, today);
        Total = total?.DisplaySigned ?? string.Empty;
        IsTotalNegative = total is { IsNegative: true };
    }

    /// <summary>Дата дня.</summary>
    public DateOnly Date { get; }

    /// <summary>Шапка: «25 августа», с годом — если день не в этом году.</summary>
    public string Title { get; }

    /// <summary>Итог дня со знаком. Пусто — итога нет: ни одна строка дня в него не входит.</summary>
    public string Total { get; }

    /// <summary>Итог отрицателен: день закончился в минус.</summary>
    public bool IsTotalNegative { get; }

    /// <summary>Год пишется только чужой: в ленте за этот год он был бы шумом в каждой шапке.</summary>
    private static string Format(DateOnly date, DateOnly today) =>
        date.Year == today.Year
            ? date.ToString("d MMMM", Russian)
            : date.ToString("d MMMM yyyy", Russian);
}
