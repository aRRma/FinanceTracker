using Finance.Application.Texts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Приводит файл базы в рабочее состояние при запуске: снимает резервную копию,
/// накатывает миграции и включает журналирование с упреждающей записью.
/// </summary>
public sealed class DatabaseBootstrapper
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly DatabaseLocation _location;

    /// <summary>
    /// Создаёт подготовку базы.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="location">Где лежит файл базы и её резервная копия.</param>
    public DatabaseBootstrapper(IDbContextFactory<FinanceDbContext> contexts, DatabaseLocation location)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(location);

        _contexts = contexts;
        _location = location;
    }

    /// <summary>
    /// Готовит базу к работе. При неудачной миграции возвращает базу из резервной
    /// копии и бросает <see cref="DatabaseMigrationException"/>: работать со старой
    /// схемой новый код не может, и запускаться приложению нельзя.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        bool existed = File.Exists(_location.Path);

        if (!await HasPendingMigrationsAsync(cancellationToken).ConfigureAwait(false))
        {
            await EnableWriteAheadLogAsync(cancellationToken).ConfigureAwait(false);

            return;
        }

        // Копия нужна только когда есть что терять: на первом запуске базы ещё нет
        if (existed)
        {
            await BackupAsync(cancellationToken).ConfigureAwait(false);
        }

        if (BeforeMigrate is { } beforeMigrate)
        {
            await beforeMigrate().ConfigureAwait(false);
        }

        try
        {
            // Контекст миграции живёт внутри и освобождается до отката: откат
            // подменяет файл базы, и открытое соединение к нему держать нельзя
            await MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Без прежней базы нечего ни возвращать, ни советовать вернуть
            // прежнюю версию: копии не было, и данных — тоже
            if (!existed)
            {
                throw new DatabaseMigrationException(
                    UiTexts.DatabaseCreateFailed,
                    error);
            }

            RestoreFromBackup();

            throw new DatabaseMigrationException(
                UiTexts.DatabaseMigrateFailed,
                error);
        }

        await EnableWriteAheadLogAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Точка для тестов: что сделать между снятием копии и миграцией. Только так
    /// проверяется сам откат из копии — миграция транзакционна, и порча базы до
    /// копии восстановилась бы и без копии.
    /// </summary>
    internal Func<Task>? BeforeMigrate { get; set; }

    private async Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<string> pending = await context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        return pending.Any();
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Снимает резервную копию. Прежняя заменяется только готовой новой: иначе
    /// сорвавшееся копирование оставило бы базу без копии, и следующей миграции
    /// откатываться было бы не на что.
    /// </summary>
    private async Task BackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await VacuumInto
                .WriteAsync(_location.ConnectionString, _location.BackupPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Схема ещё не тронута, но накатывать её без копии нельзя — данные целы
            throw new DatabaseMigrationException(
                UiTexts.DatabaseBackupFailed,
                error);
        }
    }

    /// <summary>
    /// Возвращает базу из копии, снятой перед миграцией.
    /// </summary>
    private void RestoreFromBackup()
    {
        if (!File.Exists(_location.BackupPath))
        {
            return;
        }

        // Файл базы держат открытым соединения из пула, а рядом с ним лежат журнал
        // и разделяемая память: не закрыв пул и не убрав их, мы положили бы
        // поверх копии чужое незавершённое состояние. Закрывается пул только этой
        // базы: общий сброс оборвал бы соединения с другими базами того же процесса
        using SqliteConnection own = new(_location.ConnectionString);
        SqliteConnection.ClearPool(own);

        File.Delete(_location.Path + "-wal");
        File.Delete(_location.Path + "-shm");
        File.Copy(_location.BackupPath, _location.Path, overwrite: true);
    }

    /// <summary>
    /// Включает журналирование с упреждающей записью. Режим запоминается в самом
    /// файле базы, поэтому выставляется один раз, а не на каждое соединение.
    /// </summary>
    private async Task EnableWriteAheadLogAsync(CancellationToken cancellationToken)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await context.Database
            .ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken)
            .ConfigureAwait(false);
    }
}
