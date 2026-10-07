namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Строка шкалы «Перед сбоем»: время пользователя и событие словами.
/// </summary>
public sealed class CrashTrailEntry
{
    /// <summary>
    /// Время в зоне пользователя: <c>09:55:24</c>.
    /// </summary>
    public required string Time { get; init; }

    /// <summary>
    /// Событие: «экран «О программе»», имя обработчика из кода или «сбой».
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Имя из кода — пишется моноширинным: это не слова, а метод.
    /// </summary>
    public bool IsCode { get; init; }

    /// <summary>
    /// Сам сбой — последняя строка шкалы.
    /// </summary>
    public bool IsCrash { get; init; }
}
