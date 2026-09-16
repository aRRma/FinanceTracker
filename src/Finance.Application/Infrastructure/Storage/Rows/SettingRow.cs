namespace Finance.Application.Infrastructure.Storage.Rows;

/// <summary>
/// Локальная настройка: тема, часовой пояс, номер применённой версии стартового
/// набора, последний использованный счёт.
/// </summary>
/// <remarks>
/// Единственная таблица без ключа-UUID и без полей обмена, и это намеренно:
/// настройки принадлежат устройству, а не пользователю, и между устройствами
/// не расходятся — они там просто разные.
/// </remarks>
internal sealed class SettingRow
{
    /// <summary>
    /// Имя настройки.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Значение настройки. Разбор — на стороне читающего.
    /// </summary>
    public required string Value { get; set; }
}
