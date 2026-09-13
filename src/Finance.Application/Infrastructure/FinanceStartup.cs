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

    // Замок, а не голое поле: вкладок четыре, и две могут спросить подготовку
    // одновременно — второй запуск вставил бы стартовый набор поверх первого
    private readonly Lock _gate = new();

    private Task? _prepared;

    /// <summary>Создаёт подготовку приложения.</summary>
    /// <param name="bootstrapper">Подготовка файла базы: копия, миграции, режим журнала.</param>
    /// <param name="initializer">Запись стартового набора при первом запуске.</param>
    /// <param name="settings">Локальные настройки устройства.</param>
    /// <param name="clock">Часы приложения: им задаётся часовой пояс пользователя.</param>
    public FinanceStartup(
        DatabaseBootstrapper bootstrapper,
        DatabaseInitializer initializer,
        ILocalSettings settings,
        SystemClock clock)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);

        _bootstrapper = bootstrapper;
        _initializer = initializer;
        _settings = settings;
        _clock = clock;
    }

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
            return _prepared ??= RunOnceAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Запоминается только удавшаяся подготовка. Запомни мы упавшую — приложение
    /// не поднялось бы до переустановки: повторная попытка возвращала бы ту же
    /// неудачу, даже когда мешало разовое обстоятельство вроде нехватки места.
    /// </summary>
    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        bool prepared = false;

        try
        {
            await _bootstrapper.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await ApplyTimeZoneAsync(cancellationToken).ConfigureAwait(false);

            prepared = true;
        }
        finally
        {
            if (!prepared)
            {
                lock (_gate)
                {
                    _prepared = null;
                }
            }
        }
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
}
