namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Место в ленте, с которого читается следующая страница: порядок ленты строки
/// последней показанной. Курсором, а не числом пропущенных строк: пропуск база
/// проходит заново на каждой странице, и чем глубже история, тем дольше дочитывание.
/// </summary>
/// <param name="OccurredOn">Дата операции последней показанной строки.</param>
/// <param name="CreatedAtUtc">Момент её создания — порядок внутри дня.</param>
/// <param name="Key">Её ключ — порядок при совпадении момента.</param>
public sealed record FeedCursor(DateOnly OccurredOn, DateTimeOffset CreatedAtUtc, Guid Key);
