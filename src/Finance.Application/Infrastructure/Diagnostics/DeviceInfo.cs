namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Сборка и устройство, на которых случился сбой. Их знает только платформа: прикладной слой получает готовым.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>
    /// Сведения не переданы — так бывает только вне устройства, в тестах.
    /// </summary>
    public static DeviceInfo Unknown { get; } = new() { AppVersion = "?", AppBuild = "?", Android = "?", Model = "?" };

    /// <summary>
    /// Версия приложения из манифеста.
    /// </summary>
    public required string AppVersion { get; init; }

    /// <summary>
    /// Номер сборки — <c>versionCode</c> Android.
    /// </summary>
    public required string AppBuild { get; init; }

    /// <summary>
    /// Версия Android.
    /// </summary>
    public required string Android { get; init; }

    /// <summary>
    /// Производитель и модель телефона.
    /// </summary>
    public required string Model { get; init; }
}
