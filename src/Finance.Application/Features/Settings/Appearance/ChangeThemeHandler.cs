using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Features.Settings.Appearance;

/// <summary>
/// Записывает выбранную тему и применяет её. Хранится она в базе, а не в средствах
/// платформы: настройки приложения лежат в одном месте, и резервная копия базы
/// забирает их вместе с данными.
/// </summary>
public sealed class ChangeThemeHandler : IChangeThemeHandler
{
    private readonly ILocalSettings _settings;
    private readonly ThemeApplier _applier;
    private readonly IChangeNotifier _changes;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="settings">Локальные настройки устройства.</param>
    /// <param name="applier">Применение темы платформой.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ChangeThemeHandler(ILocalSettings settings, ThemeApplier applier, IChangeNotifier changes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(changes);

        _settings = settings;
        _applier = applier;
        _changes = changes;
    }

    /// <inheritdoc />
    public async Task HandleAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        // Системная тема записывается, а не стирается: стёртая настройка
        // неотличима от ненастроенной, и следующий запуск не смог бы отличить
        // осознанный возврат к системной теме от первого запуска
        await _settings
            .SetAsync(SettingName.Theme, theme.Stored, cancellationToken)
            .ConfigureAwait(false);

        _applier.Apply(theme);

        // Не через границу транзакции: настройка пишется одной строкой без
        // доменных правил, и заворачивать её в транзакцию не во что
        _changes.Publish(DataChange.Settings);
    }
}
