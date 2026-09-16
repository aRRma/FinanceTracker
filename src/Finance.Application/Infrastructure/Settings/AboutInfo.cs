using Finance.Application.Texts;
namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Версия приложения для экрана «О программе». Её знает только платформа —
/// она лежит в манифесте, а не в прикладном слое.
/// </summary>
/// <param name="version">Версия приложения из манифеста.</param>
public sealed class AboutInfo(string? version = null)
{
    /// <summary>
    /// Версия приложения. Незаданной она бывает только вне устройства — в тестах.
    /// </summary>
    public string Version { get; } = string.IsNullOrWhiteSpace(version) ? UiTexts.AboutVersionUnknown : version;
}
