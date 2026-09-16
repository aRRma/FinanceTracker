using Finance.Domain.Enums;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Что пользователь ввёл в форме операции. Одна команда на запись и правку:
/// форма одна и та же, а отличает их только заполненный ключ. Валюты в команде нет —
/// она берётся у счёта каждой стороны.
/// </summary>
public sealed record SaveTransactionCommand
{
    /// <summary>
    /// Ключ правимой операции. Пусто — записывается новая.
    /// </summary>
    public Guid? Key { get; init; }

    /// <summary>
    /// Вид операции.
    /// </summary>
    public required TransactionKind Kind { get; init; }

    /// <summary>
    /// Счёт списания.
    /// </summary>
    public required Guid SourceAccountKey { get; init; }

    /// <summary>
    /// Сумма — уже вычисленный итог выражения из поля, в валюте счёта списания.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Счёт зачисления — у перевода.
    /// </summary>
    public Guid? TargetAccountKey { get; init; }

    /// <summary>
    /// Сумма зачисления — у перевода между валютами. При одной валюте не подаётся:
    /// она равна сумме списания, и форма её не спрашивает.
    /// </summary>
    public decimal? TargetAmount { get; init; }

    /// <summary>
    /// Подкатегория — у дохода и расхода.
    /// </summary>
    public Guid? CategoryKey { get; init; }

    /// <summary>
    /// Место названием, как набрано в форме. Совпавшее с существующим — ссылка на него,
    /// новое — заводится тут же, без похода в справочник. Пусто — без места.
    /// </summary>
    public string? PlaceName { get; init; }

    /// <summary>
    /// Дата операции.
    /// </summary>
    public required DateOnly OccurredOn { get; init; }

    /// <summary>
    /// Заметка.
    /// </summary>
    public string? Note { get; init; }
}
