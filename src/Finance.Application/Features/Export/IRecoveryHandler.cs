namespace Finance.Application.Features.Export;

/// <summary>
/// Восстановление из выгрузки: замена данных базы данными присланного файла.
/// </summary>
/// <remarks>
/// Два шага, а не один: сначала файл проверяется и называет, что заменит,
/// потом пользователь подтверждает. Замена не отменяется.
/// </remarks>
public interface IRecoveryHandler
{
    /// <summary>
    /// Принимает файл во временную папку, проверяет его и доводит до текущей схемы.
    /// Рабочая база при этом не меняется — что бы ни оказалось в файле.
    /// </summary>
    /// <param name="file">Содержимое выбранного файла.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Подходит ли файл и что в нём.</returns>
    Task<RecoveryCheck> CheckAsync(Stream file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает проверенный файл в рабочую базу одной транзакцией: при сбое
    /// база остаётся прежней. После записи приложение обязано перезапуститься —
    /// открытые экраны и запомненные при запуске настройки устарели.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="InvalidOperationException">Файл не проверен или не подошёл.</exception>
    Task RecoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет принятый файл, если пользователь передумал.
    /// </summary>
    void Discard();
}
