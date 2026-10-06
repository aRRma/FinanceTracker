using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Строка списка отчётов о сбоях: вид, когда, первая строка — и весь отчёт, который раскрывается касанием.
/// </summary>
public sealed partial class CrashReportItem : ObservableObject
{
    /// <summary>
    /// Вид сбоя словом.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Когда, в зоне пользователя, и первая строка отчёта.
    /// </summary>
    public required string Caption { get; init; }

    /// <summary>
    /// Отчёт целиком.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Отчёт раскрыт.
    /// </summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
