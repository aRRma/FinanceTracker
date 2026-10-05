using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Что происходит при запуске до первого экрана: база приводится в рабочее
/// состояние, при первом запуске записывается стартовый набор, восстанавливается
/// выбранный пользователем часовой пояс.
/// </summary>
public sealed class FinanceStartup
{
    private readonly DatabaseBootstrapper _bootstrapper;
    private readonly DatabaseInitializer _initializer;
    private readonly ILocalSettings _settings;
    private readonly SystemClock _clock;
    private readonly ThemeApplier _theme;

    // Замок, а не голое поле: вкладок четыре, и две могут спросить подготовку
    // одновременно — второй запуск вставил бы стартовый набор поверх первого
    private readonly Lock _gate = new();

    private Task? _prepared;

    /// <summary>
    /// Создаёт подготовку приложения.
    /// </summary>
    /// <param name="bootstrapper">Подготовка файла базы: копия, миграции, режим журнала.</param>
    /// <param name="initializer">Запись стартового набора при первом запуске.</param>
    /// <param name="settings">Локальные настройки устройства.</param>
    /// <param name="clock">Часы приложения: им задаётся часовой пояс пользователя.</param>
    /// <param name="theme">Применение выбранной темы оформления.</param>
    public FinanceStartup(
        DatabaseBootstrapper bootstrapper,
        DatabaseInitializer initializer,
        ILocalSettings settings,
        SystemClock clock,
        ThemeApplier theme)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(theme);

        _bootstrapper = bootstrapper;
        _initializer = initializer;
        _settings = settings;
        _clock = clock;
        _theme = theme;
    }

    /// <summary>
    /// База создана этим запуском: приложение только что установлено, а не обновлено.
    /// </summary>
    public bool CreatedDatabase { get; private set; }

    /// <summary>
    /// Готовит приложение к работе — один раз за запуск. Экраны зовут её перед
    /// первым чтением и получают одну и ту же задачу: миграции и стартовый набор
    /// не должны накатываться дважды оттого, что вкладок четыре.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены первой подготовки.</param>
    /// <exception cref="DatabaseMigrationException">Миграция не удалась; запускаться нельзя.</exception>
    public Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Запоминается только удавшаяся подготовка. Запомни мы упавшую —
            // приложение не поднялось бы до переустановки: повторная попытка
            // возвращала бы ту же неудачу, даже когда мешало разовое
            // обстоятельство вроде нехватки места. Сброс делается здесь, а не
            // внутри задачи: с SQLite весь путь бывает синхронным, и сброс
            // изнутри отработал бы раньше, чем задача попала в поле
            if (_prepared is { IsCompleted: true, IsCompletedSuccessfully: false })
            {
                _prepared = null;
            }

            return _prepared ??= RunOnceAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Один прогон подготовки: файл базы, стартовый набор, пояс и тема.
    /// </summary>
    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await _bootstrapper.InitializeAsync(cancellationToken).ConfigureAwait(false);
        CreatedDatabase = await _initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await ApplyTimeZoneAsync(cancellationToken).ConfigureAwait(false);
        await ApplyThemeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ставит часам сохранённую зону. Незнакомый идентификатор — не повод не
    /// запуститься: зона пользователя могла уехать вместе с обновлением системы,
    /// и системная зона здесь лучше отказа работать.
    /// </summary>
    private async Task ApplyTimeZoneAsync(CancellationToken cancellationToken)
    {
        string? saved = await _settings
            .GetAsync(SettingName.TimeZoneId, cancellationToken)
            .ConfigureAwait(false);

        if (saved is not null && TimeZoneInfo.TryFindSystemTimeZoneById(saved, out TimeZoneInfo? zone))
        {
            _clock.TimeZone = zone;
        }
    }

    /// <summary>
    /// Ставит сохранённую тему. Незаданная настройка — системная тема, и
    /// применить её всё равно надо: платформа помнит выбор прошлого запуска сама,
    /// и без явного сброса возврат к системной теме не подействовал бы.
    /// </summary>
    private async Task ApplyThemeAsync(CancellationToken cancellationToken)
    {
        string? saved = await _settings
            .GetAsync(SettingName.Theme, cancellationToken)
            .ConfigureAwait(false);

        _theme.Apply(Theme.Parse(saved));
    }
}
