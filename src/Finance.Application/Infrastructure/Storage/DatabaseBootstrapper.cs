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

    /// <summary>Создаёт подготовку базы.</summary>
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

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<string> pending = await context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!pending.Any())
        {
            await EnableWriteAheadLogAsync(context, cancellationToken).ConfigureAwait(false);

            return;
        }

        // Копия нужна только когда есть что терять: на первом запуске базы ещё нет
        if (existed)
        {
            await BackupAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            if (existed)
            {
                RestoreFromBackup();
            }

            throw new DatabaseMigrationException(
                "Не удалось обновить схему базы. База возвращена из резервной копии; " +
                "верните предыдущую версию приложения — данные целы",
                error);
        }

        await EnableWriteAheadLogAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Снимает целостную копию средствами СУБД. Обычное копирование файла взяло бы
    /// базу вместе с недописанным журналом, и копия оказалась бы нерабочей.
    /// </summary>
    private async Task BackupAsync(CancellationToken cancellationToken)
    {
        // Копия пишется во временный файл и подменяет прежнюю только готовой:
        // удали мы прежнюю заранее, сорвавшееся копирование — кончилось место —
        // оставило бы базу вовсе без копии, а следующая миграция откатываться
        // была бы уже не на что
        string draft = _location.BackupPath + ".tmp";

        try
        {
            File.Delete(draft);

            await using SqliteConnection connection = new(_location.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $backup";
            command.Parameters.AddWithValue("$backup", draft);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            File.Move(draft, _location.BackupPath, overwrite: true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Схема ещё не тронута, но накатывать её без копии нельзя:
            // сообщение то же, что при неудачной миграции, — данные целы
            throw new DatabaseMigrationException(
                "Не удалось снять резервную копию перед обновлением схемы; схема не тронута, данные целы",
                error);
        }
    }

    /// <summary>Возвращает базу из копии, снятой перед миграцией.</summary>
    private void RestoreFromBackup()
    {
        if (!File.Exists(_location.BackupPath))
        {
            return;
        }

        // Файл базы держат открытым соединения из пула, а рядом с ним лежат журнал
        // и разделяемая память: не закрыв пул и не убрав их, мы положили бы
        // поверх копии чужое незавершённое состояние
        SqliteConnection.ClearAllPools();

        File.Delete(_location.Path + "-wal");
        File.Delete(_location.Path + "-shm");
        File.Copy(_location.BackupPath, _location.Path, overwrite: true);
    }

    /// <summary>
    /// Включает журналирование с упреждающей записью. Режим запоминается в самом
    /// файле базы, поэтому выставляется один раз, а не на каждое соединение.
    /// </summary>
    private static async Task EnableWriteAheadLogAsync(
        FinanceDbContext context,
        CancellationToken cancellationToken) =>
        await context.Database
            .ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken)
            .ConfigureAwait(false);
}
