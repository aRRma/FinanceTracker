using Finance.Application.Features.Export;
using Microsoft.Data.Sqlite;
using static Finance.Application.Tests.AccountSetup;
using static Finance.Application.Tests.DatabaseFile;

namespace Finance.Application.Tests;

public sealed class ExportTests
{
    [Fact]
    public async Task Выгрузка_несёт_все_строки_базы_включая_удалённые_и_настройки()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
        Guid account = await SaveAsync(database, Command("Кошелёк"));
        await AddAsync(database, Expense(account, 100m));
        await AddAsync(database, Expense(account, 200m));
        await database.ExecuteAsync(
            "UPDATE transactions SET deleted_at_utc = updated_at_utc WHERE rowid = (SELECT MIN(rowid) FROM transactions)");

        string file = await database.Resolve<IExportHandler>().HandleAsync();

        IReadOnlyList<string> exported = await DumpAsync(Unpooled(file));

        Assert.Equal(await DumpAsync(database.Location.ConnectionString), exported);
        Assert.Contains(exported, static row => row.StartsWith("settings|", StringComparison.Ordinal));
        Assert.Equal(1L, await ScalarAsync(Unpooled(file), "SELECT COUNT(*) FROM transactions WHERE deleted_at_utc IS NOT NULL"));
    }

    [Fact]
    public async Task Выгрузка_берёт_строки_из_недописанного_журнала()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        // Открытое соединение не даёт закрытию последнего соединения перенести
        // журнал в файл базы: запись остаётся только в журнале
        await using SqliteConnection keeper = new(database.Location.ConnectionString);
        await keeper.OpenAsync();

        Guid account = await SaveAsync(database, Command("Кошелёк"));
        await AddAsync(database, Expense(account, 100m));

        Assert.True(new FileInfo(database.Location.Path + "-wal").Length > 0, "Запись не осталась в журнале — тест ничего не проверяет.");

        string file = await database.Resolve<IExportHandler>().HandleAsync();

        Assert.Equal(1L, await ScalarAsync(Unpooled(file), "SELECT COUNT(*) FROM transactions"));
        Assert.Equal("ok", await ScalarAsync(Unpooled(file), "PRAGMA integrity_check"));
    }

    [Fact]
    public async Task Файл_назван_сегодняшней_датой_пользователя()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        string file = await database.Resolve<IExportHandler>().HandleAsync();

        Assert.Equal("FinanceTracker-2026-09-15.db", Path.GetFileName(file));
    }

    [Fact]
    public async Task Прежние_выгрузки_удаляются()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        string first = await database.Resolve<IExportHandler>().HandleAsync();
        string stale = Path.Combine(Path.GetDirectoryName(first)!, "FinanceTracker-2026-01-01.db");
        File.Copy(first, stale);

        string second = await database.Resolve<IExportHandler>().HandleAsync();

        Assert.Equal([second], Directory.GetFiles(Path.GetDirectoryName(second)!));
    }
}
