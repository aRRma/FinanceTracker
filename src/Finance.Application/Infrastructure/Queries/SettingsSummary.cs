using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Состояние настроек одной строкой: чем подписаны строки раздела «Ещё»
/// и что показывает экран «О программе».
/// </summary>
public sealed record SettingsSummary
{
    /// <summary>
    /// Выбранная тема оформления.
    /// </summary>
    public required Theme Theme { get; init; }

    /// <summary>
    /// Часовой пояс, по которому приложение считает «сегодня».
    /// </summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// Пояс взят из системы, а не задан вручную. Разница видна только в подписи,
    /// но без неё непонятно, почему пояс сменился сам после переезда.
    /// </summary>
    public required bool TimeZoneFromSystem { get; init; }

    /// <summary>
    /// Счёт по умолчанию. Пусто — незаблокированных счетов нет, и подставлять нечего.
    /// </summary>
    public required OpenAccount? DefaultAccount { get; init; }

    /// <summary>
    /// Версия приложения из манифеста.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Номер схемы базы — число накатанных миграций. Отдельного счётчика нет
    /// намеренно: он разошёлся бы с фактическим состоянием базы при первой же
    /// забытой правке.
    /// </summary>
    public required int Schema { get; init; }
}
