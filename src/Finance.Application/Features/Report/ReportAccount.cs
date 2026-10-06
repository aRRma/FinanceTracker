using Finance.Domain.Enums;

namespace Finance.Application.Features.Report;

/// <summary>
/// Счёт для выбора счетов отчёта и для знаков его набора. Без баланса: отчёту он
/// не нужен, а балансы всех счетов — сводка по всей истории операций на каждый показ.
/// </summary>
public sealed record ReportAccount
{
    /// <summary>
    /// Ключ счёта.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Наименование счёта.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Цвет счёта — заливка знака.
    /// </summary>
    public required AccountColor Color { get; init; }

    /// <summary>
    /// Ключ значка, которым счёт рисуется: выбранный руками или по типу.
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Валюта счёта.
    /// </summary>
    public required Currency Currency { get; init; }

    /// <summary>
    /// Признак «скрытый»: счёт в накоплениях.
    /// </summary>
    public required bool IsSavings { get; init; }

    /// <summary>
    /// Счёт заблокирован.
    /// </summary>
    public required bool IsClosed { get; init; }

    /// <summary>
    /// Активный — не в накоплениях. Заблокированный тоже активный: по нему есть
    /// операции прошлых месяцев, и отчёт за них без него не сошёлся бы. То же условие
    /// в базе задаёт <see cref="Infrastructure.Queries.CountedAccounts"/>: разойдись они, знаки строки не совпали бы с суммами.
    /// </summary>
    public bool IsActive => !IsSavings;
}
