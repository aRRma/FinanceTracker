namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Где лежат база, её резервная копия и временные файлы выгрузки и восстановления. Пути приходят от
/// приложения: папки знает только платформа, а прикладной слой собирается и тестируется без неё.
/// </summary>
/// <param name="path">Полный путь к файлу базы.</param>
/// <param name="cacheFolder">Папка временных файлов. Пусто — папка рядом с базой.</param>
public sealed class DatabaseLocation(string path, string? cacheFolder = null)
{
    /// <summary>
    /// Файл базы.
    /// </summary>
    public string Path { get; } = !string.IsNullOrWhiteSpace(path)
        ? path
        : throw new ArgumentException(Faults.DatabasePathMissing(), nameof(path));

    /// <summary>
    /// Единственная резервная копия — та, что снята перед последней миграцией.
    /// Хранить историю копий незачем: откатываются всегда на шаг назад.
    /// </summary>
    public string BackupPath => Path + ".backup";

    /// <summary>
    /// Папка временных файлов: выгрузка, пока её отдают, и присланный файл, пока его проверяют.
    /// Система вправе очистить её сама, поэтому ничего долгоживущего здесь не хранится.
    /// </summary>
    public string CacheFolder { get; } = cacheFolder
        ?? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? string.Empty, "cache");

    /// <summary>
    /// Строка подключения к базе.
    /// </summary>
    public string ConnectionString => $"Data Source={Path};Default Timeout=30";
}
