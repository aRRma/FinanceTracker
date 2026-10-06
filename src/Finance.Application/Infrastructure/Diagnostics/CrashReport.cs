namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Отчёт о сбое, прочитанный из файла.
/// </summary>
public sealed class CrashReport
{
    /// <summary>
    /// Когда записан, в UTC.
    /// </summary>
    public required DateTimeOffset AtUtc { get; init; }

    /// <summary>
    /// Откуда пришёл.
    /// </summary>
    public required CrashKind Kind { get; init; }

    /// <summary>
    /// Первая строка сбоя — тип исключения. Её показывает строка списка.
    /// </summary>
    public required string Headline { get; init; }

    /// <summary>
    /// Отчёт целиком: сборка и устройство, затем сбой со стеком.
    /// </summary>
    public required string Text { get; init; }
}
