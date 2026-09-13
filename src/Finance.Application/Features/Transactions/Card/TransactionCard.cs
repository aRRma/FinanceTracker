using Finance.Domain;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Операция, как она показана в карточке правки. Ссылки отданы ключами, а место —
/// названием: в форме место набирается текстом, и ключ ей ни к чему.
/// </summary>
public sealed record TransactionCard
{
    /// <summary>Ключ операции.</summary>
    public required Guid Key { get; init; }

    /// <summary>Вид операции.</summary>
    public required TransactionKind Kind { get; init; }

    /// <summary>Счёт списания.</summary>
    public required Guid SourceAccountKey { get; init; }

    /// <summary>Счёт зачисления — у перевода.</summary>
    public required Guid? TargetAccountKey { get; init; }

    /// <summary>Сумма в валюте счёта списания.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Сумма зачисления — у перевода.</summary>
    public required decimal? TargetAmount { get; init; }

    /// <summary>Подкатегория — у дохода и расхода.</summary>
    public required Guid? CategoryKey { get; init; }

    /// <summary>Название места. Пусто, если не указано или место удалено из справочника.</summary>
    public required string? PlaceName { get; init; }

    /// <summary>Дата операции.</summary>
    public required DateOnly OccurredOn { get; init; }

    /// <summary>Заметка.</summary>
    public required string? Note { get; init; }
}
