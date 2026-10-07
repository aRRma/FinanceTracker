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
    /// Первая строка сбоя — тип исключения или причина завершения. Её показывает строка списка.
    /// </summary>
    public required string Headline { get; init; }

    /// <summary>
    /// Отчёт целиком: сборка и устройство, затем сбой со стеком.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Строка сборки и устройства: <c>app 1.0.4 (10004), android 16, Google Pixel 7</c>.
    /// </summary>
    public string Device { get; init; } = "";

    /// <summary>
    /// Строки сбоя: тип и стек, у прошлого завершения — причина, экран и трасса.
    /// </summary>
    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>
    /// След действий перед сбоем, от старых к новым: <c>09:55:24.446 nav //more/settings/about</c>, время в UTC.
    /// </summary>
    public IReadOnlyList<string> Trail { get; init; } = [];
}
