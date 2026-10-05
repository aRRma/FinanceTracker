using Finance.Application.Infrastructure.AppLock;

namespace Finance.Application.Tests;

/// <summary>
/// Телефон тестов: хранилище следа ПИН-кода, настройки вне базы и часы
/// с включения — в памяти, со временем, которое двигает сам тест.
/// </summary>
internal sealed class TestDevice : IPinStore, IDevicePreferences, IUptime
{
    private readonly Dictionary<string, long> _preferences = [];

    /// <summary>
    /// След ПИН-кода как он лежит в хранилище.
    /// </summary>
    public string? Trace { get; set; }

    /// <inheritdoc />
    public long SinceBoot { get; private set; } = 3_600_000;

    /// <inheritdoc />
    public long BootCount { get; private set; } = 7;

    /// <summary>
    /// Сдвигает часы с включения — так проходит время в фоне и во сне.
    /// </summary>
    public void Pass(TimeSpan time) => SinceBoot += (long)time.TotalMilliseconds;

    /// <summary>
    /// Перезагружает телефон: отсчёт с включения начинается заново.
    /// </summary>
    public void Reboot()
    {
        BootCount++;
        SinceBoot = 10_000;
    }

    /// <summary>
    /// Новый экземпляр защиты на этом телефоне — как после перезапуска приложения.
    /// </summary>
    public AppLockService Lock() => new(this, this, this);

    /// <inheritdoc />
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Trace);

    /// <inheritdoc />
    public Task WriteAsync(string trace, CancellationToken cancellationToken = default)
    {
        Trace = trace;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Remove() => Trace = null;

    /// <inheritdoc />
    public long? Get(string name) => _preferences.TryGetValue(name, out long value) ? value : null;

    /// <inheritdoc />
    public void Set(string name, long value) => _preferences[name] = value;

    /// <inheritdoc />
    void IDevicePreferences.Remove(string name) => _preferences.Remove(name);
}
