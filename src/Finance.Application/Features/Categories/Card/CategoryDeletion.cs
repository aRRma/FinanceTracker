namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Что будет при удалении подкатегории: сколько операций переедет и куда. Диалог
/// обязан сказать это до удаления — переезд необратим, а по числу операций видно,
/// та ли это категория.
/// </summary>
public sealed record CategoryDeletion
{
    /// <summary>Ключ удаляемой подкатегории.</summary>
    public required Guid Key { get; init; }

    /// <summary>Название удаляемой подкатегории.</summary>
    public required string Name { get; init; }

    /// <summary>Название её группы.</summary>
    public required string GroupName { get; init; }

    /// <summary>Название приёмника — подкатегории, в которую переедут операции.</summary>
    public required string ReceiverName { get; init; }

    /// <summary>Сколько операций переедет. Мягко удалённые не считаются и не переезжают.</summary>
    public required int TransactionCount { get; init; }
}
