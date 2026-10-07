using Finance.Application.Infrastructure.Diagnostics;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Строка списка отчётов о сбоях: вид, заголовок и подпись «когда · причина»; касание открывает отчёт.
/// </summary>
public sealed class CrashReportItem
{
    /// <summary>
    /// Вид сбоя — по нему выбирается значок.
    /// </summary>
    public required CrashCategory Category { get; init; }

    /// <summary>
    /// Что случилось словами: «Приложение закрылось».
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Когда, в зоне пользователя, и короткая причина.
    /// </summary>
    public required string Caption { get; init; }

    /// <summary>
    /// Над строкой линия: строки стоят в одной карточке, и первой линия не нужна.
    /// </summary>
    public bool HasDivider { get; init; }

    /// <summary>
    /// Отчёт — для экрана отчёта.
    /// </summary>
    public required CrashReport Report { get; init; }
}
