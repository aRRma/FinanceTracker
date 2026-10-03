namespace Finance.Application.Features.Export;

/// <summary>
/// Выгрузка: вся база одним файлом — для переноса на другой телефон и для переустановки.
/// </summary>
public interface IExportHandler
{
    /// <summary>
    /// Записывает базу целиком в папку временных файлов, откуда файл отдают пользователю.
    /// Прежние выгрузки удаляются: в папке лежит только последняя.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Полный путь к файлу выгрузки.</returns>
    Task<string> HandleAsync(CancellationToken cancellationToken = default);
}
