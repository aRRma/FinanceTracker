using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
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
    /// Создаёт пустую базу с накатанной схемой.
    /// </summary>
    /// <param name="applyTheme">Чем подменяется переключение оформления: платформы в тестах нет.</param>
    /// <param name="applicationVersion">Версия приложения для экрана «О программе».</param>
    public static async Task<TestDatabase> CreateAsync(
        Action<Theme>? applyTheme = null,
        string? applicationVersion = null)
    {
        TestDatabase database = CreateUnprepared(applyTheme, applicationVersion);
        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        return database;
    }

    /// <summary>
    /// Службы без единого обращения к базе: файла ещё нет. Нужно тестам первого
    /// запуска, где сама подготовка и проверяется.
    /// </summary>
    /// <param name="applyTheme">Чем подменяется переключение оформления: платформы в тестах нет.</param>
    /// <param name="applicationVersion">Версия приложения для экрана «О программе».</param>
    public static TestDatabase CreateUnprepared(
        Action<Theme>? applyTheme = null,
        string? applicationVersion = null)
    {
        string folder = Path.Combine(Path.GetTempPath(), "finance-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(folder);

        ServiceProvider services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new TestTime())
            .AddFinance(Path.Combine(folder, "finance.db"), applyTheme: applyTheme, applicationVersion: applicationVersion)
            .ConfigureDbContext<FinanceDbContext>(static options => options.AddInterceptors(NoSyncInterceptor.Instance))
            .BuildServiceProvider();

        return new TestDatabase(folder, services);
    }

    public async ValueTask DisposeAsync()
    {
        string connectionString = Location.ConnectionString;
        await _services.DisposeAsync();

        // Пул держит файл открытым: без сброса папка не удалится,
        // и временные базы будут копиться до перезагрузки. Сбрасывается только
        // свой пул: ClearAllPools закрывал соединения соседних тестов, идущих
        // параллельно, и те падали с ObjectDisposedException в Open
        using Microsoft.Data.Sqlite.SqliteConnection own = new(connectionString);
        Microsoft.Data.Sqlite.SqliteConnection.ClearPool(own);

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
