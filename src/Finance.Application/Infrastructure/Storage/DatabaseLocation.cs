namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Где лежит база и её резервная копия. Путь приходит от приложения: папку данных
/// знает только платформа, а прикладной слой собирается и тестируется без неё.
/// </summary>
/// <param name="path">Полный путь к файлу базы.</param>
public sealed class DatabaseLocation(string path)
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
    /// Строка подключения к базе.
    /// </summary>
    public string ConnectionString => $"Data Source={Path};Default Timeout=30";
}
