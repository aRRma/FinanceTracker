using Finance.Application.Infrastructure.AppLock;

namespace Finance.App;

/// <summary>
/// Настройки устройства вне базы — <see cref="Preferences"/> платформы.
/// </summary>
internal sealed class DevicePreferences : IDevicePreferences
{
    /// <inheritdoc />
    public long? Get(string name) =>
        Preferences.Default.ContainsKey(name) ? Preferences.Default.Get(name, 0L) : null;

    /// <inheritdoc />
    public void Set(string name, long value) => Preferences.Default.Set(name, value);

    /// <inheritdoc />
    public void Remove(string name) => Preferences.Default.Remove(name);
}
