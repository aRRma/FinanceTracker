using Finance.Domain.Values;

namespace Finance.Application.Features.Report;

/// <summary>
/// Операция третьего уровня, как её отдаёт база. Переводов здесь не бывает:
/// отбор идёт по подкатегории, а у перевода её нет.
/// </summary>
public sealed record ReportTransaction
{
    /// <summary>
    /// Ключ операции — по нему открывается та же карточка, что из ленты.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Дата операции.
    /// </summary>
    public required DateOnly OccurredOn { get; init; }

    /// <summary>
    /// Сумма со знаком: расход минусом, доход плюсом. В валюте счетов отчёта.
    /// </summary>
    public required Money Amount { get; init; }

    /// <summary>
    /// Счёт списания.
    /// </summary>
    public required string AccountName { get; init; }

    /// <summary>
    /// Место. Пусто, если не указано или удалено из справочника.
    /// </summary>
    public string? Place { get; init; }

    /// <summary>
    /// Заметка.
    /// </summary>
    public string? Note { get; init; }
}
