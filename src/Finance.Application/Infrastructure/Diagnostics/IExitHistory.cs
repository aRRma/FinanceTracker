namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Записи системы о прошлых завершениях процесса. Реализует платформа: историю держит Android.
/// </summary>
public interface IExitHistory
{
    /// <summary>
    /// Записи новее заданного момента, со сводкой и трассой.
    /// </summary>
    /// <param name="afterUtc">Момент последней уже разобранной записи.</param>
    /// <remarks>
    /// Трасса читается только у отданных записей: их держится до шестнадцати, и у нативного падения она
    /// весит сотни килобайт — читать все на каждом запуске незачем.
    /// </remarks>
    IReadOnlyList<PastExit> ReadAfter(DateTimeOffset afterUtc);
}
