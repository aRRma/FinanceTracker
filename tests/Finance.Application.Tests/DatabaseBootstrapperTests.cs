using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Подготовка базы при запуске: журналирование с упреждающей записью, резервная
/// копия перед миграцией и возврат из неё, когда миграция не удалась.
/// </summary>
public sealed class DatabaseBootstrapperTests
{
    /// <summary>На первом запуске терять нечего, и копия не снимается.</summary>
    [Fact]
    public async Task Первый_запуск_резервную_копию_не_снимает()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Assert.False(File.Exists(database.Location.BackupPath));
    }

    /// <summary>
    /// Режим журналирования — с упреждающей записью: без него чтение ленты
    /// блокируется записью операции.
    /// </summary>
    [Fact]
    public async Task База_открывается_в_режиме_упреждающей_записи()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";

        Assert.Equal("wal", (string?)await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Неудачная миграция возвращает базу из копии и не даёт приложению запуститься.
    /// Сбой подстроен так, как он и выглядит в жизни: схема на месте, а отметка
    /// о применённой миграции потеряна, и накат натыкается на существующие таблицы.
    /// </summary>
    [Fact]
    public async Task Неудачная_миграция_возвращает_базу_из_копии()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        int categories = await CountCategoriesAsync(database);
        Assert.NotEqual(0, categories);

        await ExecuteAsync(database, "DELETE FROM __EFMigrationsHistory");

        DatabaseMigrationException error = await Assert.ThrowsAsync<DatabaseMigrationException>(
            () => database.Resolve<DatabaseBootstrapper>().InitializeAsync());

        Assert.NotNull(error.InnerException);
        Assert.True(File.Exists(database.Location.BackupPath));

        // Копия снята до неудачного наката, поэтому категории на месте
        Assert.Equal(categories, await CountCategoriesAsync(database));
    }

    /// <summary>
    /// Откат идёт именно из копии, а не транзакцией миграции: база портится уже
    /// после снятия копии, и вернуть таблицу может только она.
    /// </summary>
    [Fact]
    public async Task Откат_возвращает_то_что_испорчено_после_снятия_копии()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        int categories = await CountCategoriesAsync(database);
        Assert.NotEqual(0, categories);

        await ExecuteAsync(database, "DELETE FROM __EFMigrationsHistory");

        DatabaseBootstrapper bootstrapper = database.Resolve<DatabaseBootstrapper>();
        bootstrapper.BeforeMigrate = () => ExecuteAsync(database, "DROP TABLE categories");

        await Assert.ThrowsAsync<DatabaseMigrationException>(() => bootstrapper.InitializeAsync());

        Assert.Equal(categories, await CountCategoriesAsync(database));
    }

    /// <summary>
    /// На первом запуске копии нет, и сообщение не обещает возврата из неё:
    /// советовать вернуть прежнюю версию, которой не было, значит послать
    /// пользователя искать несуществующее.
    /// </summary>
    [Fact]
    public async Task Сбой_первого_запуска_не_обещает_возврата_из_копии()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        DatabaseBootstrapper bootstrapper = database.Resolve<DatabaseBootstrapper>();

        // Чужая таблица с именем из схемы: накат споткнётся о неё
        bootstrapper.BeforeMigrate = () => ExecuteAsync(database, "CREATE TABLE accounts (x INTEGER)");

        DatabaseMigrationException error = await Assert.ThrowsAsync<DatabaseMigrationException>(
            () => bootstrapper.InitializeAsync());

        Assert.DoesNotContain("копии", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(database.Location.BackupPath));
    }

    /// <summary>
    /// Сорвавшееся копирование не трогает прежнюю копию и не выходит наружу
    /// сырым исключением: схема ещё цела, и пользователю говорят именно это.
    /// </summary>
    [Fact]
    public async Task Сбой_копирования_сохраняет_прежнюю_копию()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await File.WriteAllTextAsync(database.Location.BackupPath, "прежняя копия");
        await ExecuteAsync(database, "DELETE FROM __EFMigrationsHistory");

        // Папка на месте временного файла: копирование в неё невозможно
        Directory.CreateDirectory(database.Location.BackupPath + ".tmp");

        DatabaseMigrationException error = await Assert.ThrowsAsync<DatabaseMigrationException>(
            () => database.Resolve<DatabaseBootstrapper>().InitializeAsync());

        Assert.Contains("схема не тронута", error.Message, StringComparison.Ordinal);
        Assert.Equal("прежняя копия", await File.ReadAllTextAsync(database.Location.BackupPath));
    }

    private static async Task<int> CountCategoriesAsync(TestDatabase database)
    {
        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        return await context.Categories.CountAsync();
    }

    private static async Task ExecuteAsync(TestDatabase database, string sql)
    {
        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }
}
