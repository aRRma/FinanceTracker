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

    /// <summary>Создаёт пустую базу с накатанной схемой.</summary>
    public static async Task<TestDatabase> CreateAsync()
    {
        string folder = Path.Combine(Path.GetTempPath(), "finance-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(folder);

        ServiceProvider services = new ServiceCollection()
            .AddFinance(Path.Combine(folder, "finance.db"))
            .BuildServiceProvider();

        TestDatabase database = new(folder, services);
        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        return database;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();

        // Пул держит файл открытым: без сброса папка не удалится,
        // и временные базы будут копиться до перезагрузки
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

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
