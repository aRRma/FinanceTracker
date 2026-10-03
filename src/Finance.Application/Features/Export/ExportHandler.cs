using System.Globalization;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;

namespace Finance.Application.Features.Export;

/// <summary>
/// Снимает выгрузку как файл SQLite: переезжает всё как есть — идентификаторы, метки,
/// удалённые строки, настройки и версия стартового набора. Старая выгрузка
/// доводится до новой схемы теми же миграциями, что и база при обновлении.
/// </summary>
public sealed class ExportHandler : IExportHandler
{
    private const string FolderName = "export";

    private readonly DatabaseLocation _location;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="location">Где лежит база и папка временных файлов.</param>
    /// <param name="clock">Часы: дата в имени файла — «сегодня» пользователя.</param>
    public ExportHandler(DatabaseLocation location, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(clock);

        _location = location;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<string> HandleAsync(CancellationToken cancellationToken = default)
    {
        string folder = Path.Combine(_location.CacheFolder, FolderName);

        // Отданная выгрузка больше не нужна, а вся история не должна копиться
        // в папке, которую пользователь не видит и не чистит
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);

        string file = Path.Combine(
            folder,
            string.Create(CultureInfo.InvariantCulture, $"FinanceTracker-{_clock.Today:yyyy-MM-dd}.db"));

        await VacuumInto.WriteAsync(_location.ConnectionString, file, cancellationToken).ConfigureAwait(false);

        return file;
    }
}
