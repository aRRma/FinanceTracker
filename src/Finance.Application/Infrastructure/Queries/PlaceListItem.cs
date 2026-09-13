namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Место в списке — в справочнике, в его карточке и в подписи раздела «Ещё».
/// Счётчики лежат рядом с названием: по ним видно, то ли это место, ещё до
/// захода в карточку, а у дубля они и выдают дубль.
/// </summary>
public sealed record PlaceListItem
{
    /// <summary>Ключ места.</summary>
    public required Guid Key { get; init; }

    /// <summary>Название места.</summary>
    public required string Name { get; init; }

    /// <summary>Сколько операций ссылается на место.</summary>
    public required int TransactionCount { get; init; }

    /// <summary>
    /// Подкатегория, в которой место встречается чаще всего. Пусто, пока операций
    /// нет: место заводится из формы и до первого сохранения не использовано ни разу.
    /// </summary>
    public required string? TopCategoryName { get; init; }

    /// <summary>Подкатегорию есть чем подписать — строке справочника нужна вторая строка.</summary>
    public bool HasTopCategory => TopCategoryName is not null;
}
