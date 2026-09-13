namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Настройки этого устройства: тема, часовой пояс, номер применённой версии
/// стартового набора. Между устройствами не расходятся — они там просто разные,
/// поэтому полей обмена у них нет.
/// </summary>
public interface ILocalSettings
{
    /// <summary>Читает настройку. Незаданная настройка — <c>null</c>, а не пустая строка.</summary>
    /// <param name="name">Имя из <see cref="SettingName"/>.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<string?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Записывает настройку, перекрывая прежнее значение.</summary>
    /// <param name="name">Имя из <see cref="SettingName"/>.</param>
    /// <param name="value">Новое значение.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task SetAsync(string name, string value, CancellationToken cancellationToken = default);
}
