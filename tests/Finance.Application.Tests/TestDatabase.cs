using Finance.Application.Infrastructure.AppLock;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application.Tests;

/// <summary>
/// Настоящая база SQLite во временной папке. Не in-memory намеренно: проверяются
/// как раз те свойства, которых у поставщика в памяти нет, — типы колонок,
/// частичные индексы, резервная копия через <c>VACUUM INTO</c>.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _folder;
    private readonly ServiceProvider _services;

    private TestDatabase(string folder, ServiceProvider services)
    {
        _folder = folder;
        _services = services;
    }

    public DatabaseLocation Location => _services.GetRequiredService<DatabaseLocation>();

    public IDbContextFactory<FinanceDbContext> Contexts =>
        _services.GetRequiredService<IDbContextFactory<FinanceDbContext>>();

    public T Resolve<T>() where T : notnull => _services.GetRequiredService<T>();

    /// <summary>
    /// Накатывает схему до названной миграции включительно, а с более поздней —
    /// откатывает до неё: так тест получает базу, какой её оставило прежнее приложение.
    /// </summary>
    /// <param name="migration">Имя миграции.</param>
    public async Task MigrateToAsync(string migration)
    {
        await using FinanceDbContext context = await Contexts.CreateDbContextAsync();

        await context.GetService<IMigrator>().MigrateAsync(migration);
    }

    /// <summary>
    /// Выполняет SQL в обход EF: так подкладывают строки, какими их писало прежнее
    /// приложение, и портят базу, которую приложение само испортить не даст.
    /// </summary>
    /// <param name="sql">Команды SQL.</param>
    public async Task ExecuteAsync(string sql)
    {
        await using SqliteConnection connection = new(Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Создаёт пустую базу с накатанной схемой.
    /// </summary>
    /// <param name="applyTheme">Чем подменяется переключение оформления: платформы в тестах нет.</param>
    /// <param name="applicationVersion">Версия приложения для экрана «О программе».</param>
    /// <param name="extra">Службы сверх прикладного слоя — например, перенос, которого в приложении нет.</param>
    public static async Task<TestDatabase> CreateAsync(
        Action<Theme>? applyTheme = null,
        string? applicationVersion = null,
        Action<IServiceCollection>? extra = null)
    {
        TestDatabase database = CreateUnprepared(applyTheme, applicationVersion, extra);
        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        return database;
    }

    /// <summary>
    /// Создаёт базу с накатанной схемой и стартовым набором — как после первого запуска.
    /// </summary>
    /// <param name="applicationVersion">Версия приложения для экрана «О программе».</param>
    /// <param name="extra">Службы сверх прикладного слоя.</param>
    public static async Task<TestDatabase> CreateWithPresetAsync(
        string? applicationVersion = null,
        Action<IServiceCollection>? extra = null)
    {
        TestDatabase database = await CreateAsync(applicationVersion: applicationVersion, extra: extra);
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        return database;
    }

    /// <summary>
    /// Службы без единого обращения к базе: файла ещё нет. Нужно тестам первого
    /// запуска, где сама подготовка и проверяется.
    /// </summary>
    /// <param name="applyTheme">Чем подменяется переключение оформления: платформы в тестах нет.</param>
    /// <param name="applicationVersion">Версия приложения для экрана «О программе».</param>
    /// <param name="extra">Службы сверх прикладного слоя.</param>
    public static TestDatabase CreateUnprepared(
        Action<Theme>? applyTheme = null,
        string? applicationVersion = null,
        Action<IServiceCollection>? extra = null)
    {
        string folder = Path.Combine(Path.GetTempPath(), "finance-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(folder);

        // Телефона в тестах нет: защита входа хранит своё в памяти
        TestDevice device = new();

        IServiceCollection collection = new ServiceCollection()
            .AddSingleton<TimeProvider>(new TestTime())
            .AddSingleton(device)
            .AddSingleton<IPinStore>(device)
            .AddSingleton<IDevicePreferences>(device)
            .AddSingleton<IUptime>(device)
            .AddFinance(Path.Combine(folder, "finance.db"), applyTheme: applyTheme, applicationVersion: applicationVersion)
            .ConfigureDbContext<FinanceDbContext>(static options => options.AddInterceptors(NoSyncInterceptor.Instance));
        extra?.Invoke(collection);

        return new TestDatabase(folder, collection.BuildServiceProvider());
    }

    public async ValueTask DisposeAsync()
    {
        string connectionString = Location.ConnectionString;
        await _services.DisposeAsync();

        // Пул держит файл открытым: без сброса папка не удалится,
        // и временные базы будут копиться до перезагрузки. Сбрасывается только
        // свой пул: ClearAllPools закрывал соединения соседних тестов, идущих
        // параллельно, и те падали с ObjectDisposedException в Open
        using SqliteConnection own = new(connectionString);
        SqliteConnection.ClearPool(own);

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Мусор во временной папке не повод ронять тест
        }
    }
}
