namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Запись системы о прошлом завершении процесса — что Android отдаёт при следующем запуске.
/// </summary>
public sealed class PastExit
{
    /// <summary>
    /// Когда процесс завершился.
    /// </summary>
    public required DateTimeOffset AtUtc { get; init; }

    /// <summary>
    /// Причина.
    /// </summary>
    public required ExitReason Reason { get; init; }

    /// <summary>
    /// Код выхода или номер сигнала.
    /// </summary>
    public required int Status { get; init; }

    /// <summary>
    /// Описание от системы; пусто — система не дала.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Сводка, которую приложение отдало системе заранее: экран и последнее действие.
    /// </summary>
    public byte[]? Summary { get; init; }

    /// <summary>
    /// Трасса: у нативного падения — protobuf, у зависания — текст; у остальных — пусто.
    /// </summary>
    public byte[]? Trace { get; init; }
}
