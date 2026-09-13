namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Место в подсказках формы. Порядок задаёт частота использования, а не алфавит:
/// магазин, куда ходят каждый день, должен быть первым.
/// </summary>
/// <param name="Key">Ключ места.</param>
/// <param name="Name">Название.</param>
/// <param name="UsageCount">Сколько операций на него ссылаются.</param>
public sealed record PlaceOption(Guid Key, string Name, int UsageCount);
