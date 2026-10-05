namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Защита входа: задан ли ПИН-код, верен ли набранный, нужен ли вход сейчас.
/// Один на приложение — состояние «вход не выполнен» живёт столько же, сколько процесс.
/// </summary>
/// <remarks>
/// Всё хранится на телефоне вне базы: след кода — в зашифрованном хранилище,
/// остальное — в настройках устройства. С выгрузкой защита не переезжает.
/// </remarks>
public sealed class AppLockService
{
    /// <summary>
    /// Цифр в ПИН-коде — всегда четыре, выбора длины нет (решение пользователя).
    /// Код проверяется по набору последней цифры.
    /// </summary>
    public const int PinLength = 4;

    /// <summary>
    /// Сколько приложение может пробыть в фоне без нового входа.
    /// </summary>
    public static readonly TimeSpan BackgroundLimit = TimeSpan.FromMinutes(5);

    private const string EnabledName = "app_lock.enabled";
    private const string FailuresName = "app_lock.failures";
    private const string PauseFromName = "app_lock.pause_from";
    private const string PauseBootName = "app_lock.pause_boot";
    private const string PauseLengthName = "app_lock.pause_length";

    private readonly IPinStore _store;
    private readonly IDevicePreferences _preferences;
    private readonly IUptime _uptime;

    private (long At, long Boot)? _left;
    private bool _started;

    /// <summary>
    /// Создаёт защиту входа.
    /// </summary>
    /// <param name="store">Хранилище следа ПИН-кода.</param>
    /// <param name="preferences">Настройки устройства вне базы.</param>
    /// <param name="uptime">Время с включения телефона.</param>
    public AppLockService(IPinStore store, IDevicePreferences preferences, IUptime uptime)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(uptime);

        _store = store;
        _preferences = preferences;
        _uptime = uptime;
    }

    /// <summary>
    /// ПИН-код задан.
    /// </summary>
    public bool IsEnabled => _preferences.Get(EnabledName) is 1;

    /// <summary>
    /// Вход не выполнен: приложение закрыто заслонкой.
    /// </summary>
    /// <remarks>
    /// Поднятый признак снимает только верный код. Иначе второй уход в фон
    /// с заслонкой на экране начал бы отсчёт заново, и через минуту возврат
    /// снял бы заслонку без всякого кода.
    /// </remarks>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Годится ли набранное как ПИН-код: ровно четыре цифры.
    /// </summary>
    /// <param name="pin">Набранное.</param>
    public static bool IsValid(string? pin) =>
        pin is { Length: PinLength } && pin.All(char.IsAsciiDigit);

    /// <summary>
    /// Создание окна приложения. Первое за жизнь процесса — запуск, и при включённой
    /// защите вход нужен всегда. Следующие — пересоздание в живом процессе (смена языка
    /// или масштаба шрифта, возврат после выхода «назад»): решают как возврат из фона.
    /// </summary>
    /// <remarks>
    /// Иначе смена масштаба шрифта в настройках телефона запирала бы приложение,
    /// которое было в фоне секунды.
    /// </remarks>
    /// <returns>Нужен ли вход.</returns>
    public bool Start()
    {
        if (_started)
        {
            return Return();
        }

        _started = true;
        IsLocked = IsEnabled;

        return IsLocked;
    }

    /// <summary>
    /// Приложение ушло в фон: запоминается, когда — по часам, идущим во сне.
    /// </summary>
    public void Leave()
    {
        if (!IsLocked)
        {
            _left = (_uptime.SinceBoot, _uptime.BootCount);
        }
    }

    /// <summary>
    /// Приложение вернулось из фона: вход нужен, если оно пробыло там пять минут и дольше.
    /// </summary>
    /// <returns>Нужен ли вход.</returns>
    public bool Return()
    {
        if (IsLocked || !IsEnabled)
        {
            return IsLocked;
        }

        // Ухода не видели или телефон перезагружался — длительность неизвестна,
        // и решает осторожность
        IsLocked = _left is not { } left
            || left.Boot != _uptime.BootCount
            || _uptime.SinceBoot < left.At
            || TimeSpan.FromMilliseconds(_uptime.SinceBoot - left.At) >= BackgroundLimit;

        return IsLocked;
    }

    /// <summary>
    /// Задаёт новый ПИН-код и включает защиту. Смена кода — то же задание.
    /// </summary>
    /// <param name="pin">Новый код.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="ArgumentException">Код не из четырёх цифр.</exception>
    public async Task EnableAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (!IsValid(pin))
        {
            throw new ArgumentException("PIN must be 4 digits.", nameof(pin));
        }

        // Медленная функция нарочно медленная — вне потока интерфейса
        string trace = await Task.Run(() => PinTrace.Create(pin).ToString(), cancellationToken).ConfigureAwait(false);

        await _store.WriteAsync(trace, cancellationToken).ConfigureAwait(false);

        _preferences.Set(EnabledName, 1);

        ResetFailures();
    }

    /// <summary>
    /// Выключает защиту и стирает след кода.
    /// </summary>
    public void Disable()
    {
        _store.Remove();

        _preferences.Remove(EnabledName);

        ResetFailures();

        IsLocked = false;
    }

    /// <summary>
    /// Проверяет набранный код. Верный снимает заслонку и сбрасывает счёт неверных,
    /// неверный засчитывается и с пятого раза ставит паузу.
    /// </summary>
    /// <param name="pin">Набранный код.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task<PinCheck> CheckAsync(string pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        TimeSpan pause = PauseRemaining();

        if (pause > TimeSpan.Zero)
        {
            return new PinCheck(PinCheckOutcome.Paused, 0, pause);
        }

        PinTrace? trace = PinTrace.Parse(await _store.ReadAsync(cancellationToken).ConfigureAwait(false));

        // Следа нет — хранилище потеряло ключ Android. Сверить код не с чем, и
        // заслонка не снялась бы никогда: защита выключается, а данные остаются
        // доступны своему же пользователю. Стереть след в обход приложения можно
        // только со взломом телефона, а от него защита входа и не обещает спасти
        if (trace is null)
        {
            Disable();

            return new PinCheck(PinCheckOutcome.Accepted, EntryPause.FreeAttempts, TimeSpan.Zero);
        }

        if (await Task.Run(() => trace.Matches(pin), cancellationToken).ConfigureAwait(false))
        {
            ResetFailures();

            IsLocked = false;
            _left = null;

            return new PinCheck(PinCheckOutcome.Accepted, EntryPause.FreeAttempts, TimeSpan.Zero);
        }

        long failures = (_preferences.Get(FailuresName) ?? 0) + 1;

        _preferences.Set(FailuresName, failures);

        TimeSpan next = EntryPause.After((int)Math.Min(failures, int.MaxValue));

        if (next > TimeSpan.Zero)
        {
            _preferences.Set(PauseFromName, _uptime.SinceBoot);
            _preferences.Set(PauseBootName, _uptime.BootCount);
            _preferences.Set(PauseLengthName, (long)next.TotalMilliseconds);
        }

        return new PinCheck(PinCheckOutcome.Rejected, (int)Math.Max(0, EntryPause.FreeAttempts - failures), next);
    }

    /// <summary>
    /// Сколько осталось ждать до следующей попытки.
    /// </summary>
    /// <remarks>
    /// После перезагрузки телефона отсчёт с её начала не сравнить с прежним,
    /// и пауза начинается заново целиком: иначе перезагрузка снимала бы её.
    /// </remarks>
    public TimeSpan PauseRemaining()
    {
        if (_preferences.Get(PauseLengthName) is not long length
            || _preferences.Get(PauseFromName) is not long from
            || _preferences.Get(PauseBootName) is not long boot)
        {
            return TimeSpan.Zero;
        }

        long now = _uptime.SinceBoot;

        if (boot != _uptime.BootCount || now < from)
        {
            _preferences.Set(PauseFromName, now);
            _preferences.Set(PauseBootName, _uptime.BootCount);

            return TimeSpan.FromMilliseconds(length);
        }

        long left = length - (now - from);

        return left > 0 ? TimeSpan.FromMilliseconds(left) : TimeSpan.Zero;
    }

    private void ResetFailures()
    {
        _preferences.Remove(FailuresName);
        _preferences.Remove(PauseFromName);
        _preferences.Remove(PauseBootName);
        _preferences.Remove(PauseLengthName);
    }
}
