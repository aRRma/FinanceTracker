using System.Text;
using Finance.Application.Features.Export;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Texts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static Finance.Application.Tests.AccountSetup;
using static Finance.Application.Tests.DatabaseFile;

namespace Finance.Application.Tests;

public sealed class RecoveryTests
{
    [Fact]
    public async Task Выгрузка_заменяет_данные_базы_целиком()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        Guid wallet = await SaveAsync(source, Command("Кошелёк"));
        await AddAsync(source, Expense(wallet, 100m));
        await AddAsync(source, Expense(wallet, 200m));
        await source.ExecuteAsync(
            "UPDATE transactions SET deleted_at_utc = updated_at_utc WHERE rowid = (SELECT MIN(rowid) FROM transactions);" +
            "INSERT INTO settings (name, value) VALUES ('theme', 'Dark')");
        string file = await source.Resolve<IExportHandler>().HandleAsync();

        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        Guid card = await SaveAsync(target, Command("Карта"));
        await AddAsync(target, Expense(card, 50m));
        await AddAsync(target, Expense(card, 60m));
        await AddAsync(target, Expense(card, 70m));
        IRecoveryHandler recovery = target.Resolve<IRecoveryHandler>();

        RecoveryCheck check = await CheckAsync(recovery, file);
        await recovery.RecoverAsync();

        Assert.Equal(RecoveryVerdict.Ready, check.Verdict);
        Assert.Equal(new RecoverySide { Accounts = 1, Transactions = 3 }, check.Current);
        Assert.Equal(await DumpAsync(source.Location.ConnectionString), await DumpAsync(target.Location.ConnectionString));
        Assert.False(Directory.Exists(RecoveryFolder(target)), "Принятый файл остался в кэше.");
    }

    [Fact]
    public async Task Выгрузка_старой_схемы_доводится_до_текущей()
    {
        await using TestDatabase source = TestDatabase.CreateUnprepared();
        await source.MigrateToAsync("20260914141411_LedgerFeedIndex");
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        IRecoveryHandler recovery = target.Resolve<IRecoveryHandler>();

        RecoveryCheck check = await CheckAsync(recovery, file);
        await recovery.RecoverAsync();

        Assert.Equal(RecoveryVerdict.Ready, check.Verdict);
        await using FinanceDbContext context = await target.Contexts.CreateDbContextAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Просто текст, а не база")]
    [InlineData("\u0089PNG\r\n\u001a\n картинка")]
    public async Task Чужой_файл_отвергается(string content)
    {
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();

        RecoveryCheck check = await RefuseAsync(target, new MemoryStream(Encoding.UTF8.GetBytes(content)));

        Assert.Equal(RecoveryVerdict.NotExport, check.Verdict);
    }

    [Fact]
    public async Task База_без_нашей_истории_миграций_отвергается()
    {
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        string foreign = Path.Combine(target.Location.CacheFolder, "..", "foreign.db");
        await ExecuteAsync(Unpooled(foreign), "CREATE TABLE notes (text TEXT); INSERT INTO notes VALUES ('x')");

        RecoveryCheck check = await RefuseAsync(target, new MemoryStream(await File.ReadAllBytesAsync(foreign)));

        Assert.Equal(RecoveryVerdict.NotExport, check.Verdict);
    }

    [Fact]
    public async Task Выгрузка_из_новой_версии_отвергается()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await ExecuteAsync(
            Unpooled(file),
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_Future', '99.0.0')");
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();

        RecoveryCheck check = await RefuseAsync(target, new MemoryStream(await File.ReadAllBytesAsync(file)));

        Assert.Equal(RecoveryVerdict.Newer, check.Verdict);
    }

    [Fact]
    public async Task Испорченная_выгрузка_отвергается()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        byte[] bytes = await File.ReadAllBytesAsync(file);
        int pageSize = Convert.ToInt32(await ScalarAsync(Unpooled(file), "PRAGMA page_size"));

        // Затирается вторая страница целиком: заголовок файла цел, и SQLite
        // принимает файл за базу, а не за чужой файл
        Array.Fill(bytes, (byte)0xA5, pageSize, pageSize);

        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();

        RecoveryCheck check = await RefuseAsync(target, new MemoryStream(bytes));

        Assert.Equal(RecoveryVerdict.Damaged, check.Verdict);
    }

    [Fact]
    public async Task Сорванная_запись_оставляет_базу_прежней()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(source, Command("Кошелёк"));
        string file = await source.Resolve<IExportHandler>().HandleAsync();

        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(target, Command("Карта"));
        IReadOnlyList<string> before = await DumpAsync(target.Location.ConnectionString);
        RecoveryHandler recovery = (RecoveryHandler)target.Resolve<IRecoveryHandler>();
        await CheckAsync(recovery, file);

        // Рабочая база в журнальном режиме принимает копию только с тем же
        // размером страницы: иной размер роняет саму запись, а не проверку
        await ExecuteAsync(Unpooled(recovery.IncomingPath), "PRAGMA page_size = 8192; VACUUM;");

        await Assert.ThrowsAsync<SqliteException>(() => recovery.RecoverAsync());

        Assert.Equal(before, await DumpAsync(target.Location.ConnectionString));
    }

    [Fact]
    public async Task Предупреждение_называет_что_будет_удалено()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        Guid card = await SaveAsync(target, Command("Карта"));
        await AddAsync(target, Expense(card, 50m));
        await AddAsync(target, Expense(card, 60m));
        ExportViewModel model = target.Resolve<ExportViewModel>();

        await using FileStream stream = File.OpenRead(file);
        bool ready = await model.CheckAsync(stream);

        Assert.True(ready);
        Assert.Equal("1 счёт", model.AccountsToDelete);
        Assert.Equal("2 операции", model.TransactionsToDelete);
        Assert.True(model.ReplacesData);
    }

    [Fact]
    public async Task Счёт_без_операций_тоже_требует_предупреждения()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(target, Command("Карта"));
        ExportViewModel model = target.Resolve<ExportViewModel>();

        await using FileStream stream = File.OpenRead(file);
        await model.CheckAsync(stream);

        Assert.True(model.ReplacesData);
        Assert.Equal("0 операций", model.TransactionsToDelete);
    }

    [Fact]
    public async Task Пустая_база_восстанавливается_без_предупреждения()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(source, Command("Кошелёк"));
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        ExportViewModel model = target.Resolve<ExportViewModel>();
        await using (FileStream stream = File.OpenRead(file))
        {
            await model.CheckAsync(stream);
        }

        await model.RecoverAsync();

        Assert.False(model.ReplacesData);
        Assert.Equal(await DumpAsync(source.Location.ConnectionString), await DumpAsync(target.Location.ConnectionString));
    }

    [Fact]
    public async Task Замену_нельзя_подтвердить_до_конца_отсчёта()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(target, Command("Карта"));
        IReadOnlyList<string> before = await DumpAsync(target.Location.ConnectionString);
        ExportViewModel model = target.Resolve<ExportViewModel>();
        await using (FileStream stream = File.OpenRead(file))
        {
            await model.CheckAsync(stream);
        }

        model.ShowWarning();

        Assert.False(model.CanConfirm);
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.RecoveryConfirmWaiting, ExportViewModel.WarningSeconds), model.ConfirmButtonText);
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.RecoverAsync());
        Assert.Equal(before, await DumpAsync(target.Location.ConnectionString));

        bool[] ticks = [.. Enumerable.Range(0, ExportViewModel.WarningSeconds).Select(_ => model.Tick())];

        Assert.Equal([true, true, true, true, false], ticks);
        Assert.True(model.CanConfirm);
        Assert.Equal(UiTexts.RecoveryConfirm, model.ConfirmButtonText);
        await model.RecoverAsync();
    }

    [Fact]
    public async Task Отмена_закрывает_предупреждение()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(target, Command("Карта"));
        ExportViewModel model = target.Resolve<ExportViewModel>();
        await using (FileStream stream = File.OpenRead(file))
        {
            await model.CheckAsync(stream);
        }

        model.ShowWarning();
        model.Discard();

        Assert.False(model.IsWarningShown);
        Assert.True(model.IsActionsShown);
        Assert.False(model.Tick());
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.RecoverAsync());
    }

    [Fact]
    public async Task Отказ_называет_причину()
    {
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        ExportViewModel model = target.Resolve<ExportViewModel>();

        bool ready = await model.CheckAsync(new MemoryStream(Encoding.UTF8.GetBytes("Просто текст")));

        Assert.False(ready);
        Assert.Equal(UiTexts.RecoveryNotExport, model.RefusalText);
        Assert.Empty(model.AccountsToDelete);
    }

    [Fact]
    public async Task Оборванный_файл_не_остаётся_и_не_записывается()
    {
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        IRecoveryHandler recovery = target.Resolve<IRecoveryHandler>();

        await Assert.ThrowsAsync<IOException>(() => recovery.CheckAsync(new BrokenStream()));

        Assert.False(Directory.Exists(RecoveryFolder(target)), "Недописанный файл остался в кэше.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.RecoverAsync());
    }

    [Fact]
    public async Task Отвергнутый_после_подошедшего_файл_не_записывается()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        IRecoveryHandler recovery = target.Resolve<IRecoveryHandler>();
        await CheckAsync(recovery, file);

        await recovery.CheckAsync(new MemoryStream(Encoding.UTF8.GetBytes("Просто текст")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.RecoverAsync());
    }

    [Fact]
    public async Task Сбой_проверки_стирает_тексты_прошлого_файла()
    {
        await using TestDatabase source = await TestDatabase.CreateWithPresetAsync();
        string file = await source.Resolve<IExportHandler>().HandleAsync();
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        ExportViewModel model = target.Resolve<ExportViewModel>();
        await using (FileStream stream = File.OpenRead(file))
        {
            await model.CheckAsync(stream);
        }

        await Assert.ThrowsAsync<IOException>(() => model.CheckAsync(new BrokenStream()));

        Assert.Empty(model.AccountsToDelete);
        Assert.Empty(model.RefusalText);
    }

    [Fact]
    public async Task Без_проверки_восстанавливать_нечего()
    {
        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => target.Resolve<IRecoveryHandler>().RecoverAsync());
    }

    private static async Task<RecoveryCheck> CheckAsync(IRecoveryHandler recovery, string file)
    {
        await using FileStream stream = File.OpenRead(file);

        return await recovery.CheckAsync(stream);
    }

    /// <summary>
    /// Проверяет файл, который обязан быть отвергнут, и сверяет, что отказ
    /// ничего не тронул: рабочая база прежняя, принятый файл удалён.
    /// </summary>
    private static async Task<RecoveryCheck> RefuseAsync(TestDatabase target, Stream file)
    {
        IReadOnlyList<string> before = await DumpAsync(target.Location.ConnectionString);

        RecoveryCheck check = await target.Resolve<IRecoveryHandler>().CheckAsync(file);

        Assert.Null(check.Current);
        Assert.Equal(before, await DumpAsync(target.Location.ConnectionString));
        Assert.False(Directory.Exists(RecoveryFolder(target)), "Отвергнутый файл остался в кэше.");

        return check;
    }

    private static string RecoveryFolder(TestDatabase database) =>
        Path.Combine(database.Location.CacheFolder, "recovery");

    /// <summary>
    /// Поток, который отдаёт начало файла и обрывается: так ведёт себя выбор файла,
    /// когда кончилось место или пропала связь с облачным диском.
    /// </summary>
    private sealed class BrokenStream : Stream
    {
        private bool _started;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_started)
            {
                throw new IOException("Поток оборвался");
            }

            _started = true;
            "SQLite format 3\0"u8.CopyTo(buffer);

            return 16;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
