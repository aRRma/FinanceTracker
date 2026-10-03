namespace Finance.Application.Features.Export;

/// <summary>
/// Итог проверки присланного файла: подходит ли он и что в базе будет заменено.
/// </summary>
public sealed record RecoveryCheck
{
    /// <summary>
    /// Подходит ли файл.
    /// </summary>
    public required RecoveryVerdict Verdict { get; init; }

    /// <summary>
    /// Что в рабочей базе сейчас — то, что замена удалит. Пусто, если файл не подошёл.
    /// </summary>
    public RecoverySide? Current { get; init; }
}
