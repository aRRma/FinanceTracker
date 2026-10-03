using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Export;

/// <summary>
/// Восстанавливает базу из выгрузки. Файл проверяется и доводится до текущей
/// схемы во временной папке, и только готовый переносится в рабочую базу
/// встроенным копированием SQLite.
/// </summary>
/// <remarks>
/// Порядок — главное: мигрируй мы выгрузку уже на месте рабочей базы, неудача
/// оставила бы приложение без данных — текущие стёрты, присланные не открываются.
/// </remarks>
public sealed class RecoveryHandler : IRecoveryHandler
{
    private const string FolderName = "recovery";

    // Коды ошибок SQLite: файл не база вовсе и база с испорченными страницами
    private const int NotADatabase = 26;
    private const int Corrupt = 11;

    private readonly DatabaseLocation _location;
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly IClock _clock;

    // Принятый файл прошёл проверку и доведён до текущей схемы: только такой пишется в базу
    private bool _checked;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="location">Где лежит база и папка временных файлов.</param>
    /// <param name="contexts">Фабрика контекстов рабочей базы.</param>
    /// <param name="clock">Часы: их требует контекст над присланным файлом.</param>
    public RecoveryHandler(DatabaseLocation location, IDbContextFactory<FinanceDbContext> contexts, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(clock);

        _location = location;
        _contexts = contexts;
        _clock = clock;
    }

    /// <summary>
    /// Принятый файл. Открыт для тестов: только так проверяется отказ самой записи.
    /// </summary>
    internal string IncomingPath => Path.Combine(Folder, "incoming.db");

    private string Folder => Path.Combine(_location.CacheFolder, FolderName);

    // Без пула: пул держал бы файл открытым после проверки, и удалить его не вышло бы
    private string IncomingConnectionString =>
        new SqliteConnectionStringBuilder { DataSource = IncomingPath, Pooling = false }.ToString();

    /// <inheritdoc />
    public async Task<RecoveryCheck> CheckAsync(Stream file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        Discard();
        Directory.CreateDirectory(Folder);

        try
        {
            // SQLite нужен файл, а выбор файла отдаёт поток. Копирование — внутри
            // try: оборванное, оно оставило бы в кэше недописанный файл
            await using (FileStream copy = File.Create(IncomingPath))
            {
                await file.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
            }

            RecoveryVerdict verdict = await InspectAsync(cancellationToken).ConfigureAwait(false);

            if (verdict is not RecoveryVerdict.Ready)
            {
                return new RecoveryCheck { Verdict = verdict };
            }

            await using (FinanceDbContext context = OpenIncoming())
            {
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }

            RecoverySide current = await ReadCurrentAsync(cancellationToken).ConfigureAwait(false);

            _checked = true;

            return new RecoveryCheck { Verdict = RecoveryVerdict.Ready, Current = current };
        }
        finally
        {
            // Личные данные из неподошедшего файла в кэше не остаются
            if (!_checked)
            {
                Discard();
            }
        }
    }

    /// <inheritdoc />
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        // Признак, а не наличие файла: файл появляется до проверки, и
        // недописанный или отвергнутый попал бы в рабочую базу
        if (!_checked)
        {
            throw new InvalidOperationException(Faults.RecoveryNotChecked());
        }

        // Копирование SQLite синхронное и на многолетней истории заметно: вне потока интерфейса
        await Task.Run(CopyIncomingToDatabase, cancellationToken).ConfigureAwait(false);

        try
        {
            Discard();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Данные уже заменены: неубранный файл в кэше — не повод сообщать об ошибке
            // и пропускать перезапуск, без которого экраны описывают прежние данные.
            // Признак проверки Discard снял первым делом — повторно файл не запишется
        }
    }

    /// <inheritdoc />
    public void Discard()
    {
        _checked = false;

        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }

    /// <summary>
    /// Отличает выгрузку трекера от чужого и испорченного файла. EF сам базу из
    /// будущей версии не отвергает: он накатывает известные ему миграции и молча
    /// пропускает лишние строки истории, — поэтому история сверяется здесь.
    /// </summary>
    private async Task<RecoveryVerdict> InspectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using SqliteConnection connection = new(IncomingConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";

            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not "ok")
            {
                return RecoveryVerdict.Damaged;
            }
        }
        catch (SqliteException error) when (error.SqliteErrorCode is NotADatabase)
        {
            return RecoveryVerdict.NotExport;
        }
        catch (SqliteException error) when (error.SqliteErrorCode is Corrupt)
        {
            return RecoveryVerdict.Damaged;
        }

        await using FinanceDbContext context = OpenIncoming();

        string[] migrations = [.. context.Database.GetMigrations()];
        HashSet<string> known = [.. migrations];
        IEnumerable<string> applied = await context.Database
            .GetAppliedMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);
        string[] history = [.. applied];

        // Наша первая миграция — признак, что база вообще трекера: чужая база
        // SQLite открывается так же успешно
        if (!history.Contains(migrations[0]))
        {
            return RecoveryVerdict.NotExport;
        }

        return history.All(known.Contains) ? RecoveryVerdict.Ready : RecoveryVerdict.Newer;
    }

    private FinanceDbContext OpenIncoming() =>
        new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(IncomingConnectionString).Options, _clock);

    /// <summary>
    /// Считает, что в рабочей базе будет удалено.
    /// </summary>
    private async Task<RecoverySide> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        await using FinanceDbContext context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return new RecoverySide
        {
            Accounts = await context.Accounts.CountAsync(cancellationToken).ConfigureAwait(false),
            Transactions = await context.Transactions.CountAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Переносит принятый файл в рабочую базу. Встроенное копирование SQLite
    /// пишет одной транзакцией прямо на ходу: при сбое база остаётся прежней,
    /// и ни закрывать пул, ни убирать журнал рядом с базой не нужно.
    /// </summary>
    private void CopyIncomingToDatabase()
    {
        using SqliteConnection source = new(IncomingConnectionString);
        source.Open();

        using SqliteConnection target = new(_location.ConnectionString);
        target.Open();

        source.BackupDatabase(target);
    }
}
