using System.Text;
using System.Text.Json;
using Finance.Application.Features.Export;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Import;
using Finance.Import.Wallet;
using Microsoft.EntityFrameworkCore;

using static Finance.Application.Tests.AccountSetup;
using static Finance.Application.Tests.DatabaseFile;

namespace Finance.Application.Tests;

/// <summary>
/// Разовый перенос из Wallet: запись одной транзакцией тем же путём, что у форм,
/// только в пустую базу, со строгим разбором файла.
/// </summary>
public sealed class WalletImportTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2025, 3, 10, 7, 15, 0, TimeSpan.Zero);

    /// <summary>
    /// Файл на два счёта: расход с местом, зарплата, перевод на скрытый счёт
    /// и возврат в расходную статью.
    /// </summary>
    private static WalletImportFile Sample(params WalletImportTransaction[] extra) => new(
        WalletImportFile.FormatName,
        WalletImportFile.CurrentVersion,
        [
            new("Карта", AccountType.Card, Currency.RUB, 100m, new DateOnly(2025, 1, 1), ExcludedFromTotals: false),
            new("Запас", AccountType.Cash, Currency.RUB, 0m, new DateOnly(2025, 1, 1), ExcludedFromTotals: true),
        ],
        ["Пятёрочка"],
        [
            Purchase("Карта", 30m, "food.groceries", "Пятёрочка"),
            new(TransactionKind.Income, "Карта", null, 1000m, "income.salary", null, new DateOnly(2025, 3, 5), RecordedAtUtc, null),
            new(TransactionKind.Transfer, "Карта", "Запас", 200m, null, null, new DateOnly(2025, 3, 6), RecordedAtUtc, null),
            new(TransactionKind.Income, "Карта", null, 5m, "food.groceries", null, new DateOnly(2025, 3, 7), RecordedAtUtc, "Wallet: Еда · Ашан"),
            .. extra,
        ]);

    /// <summary>
    /// База со стартовым набором и переносом: в приложении переноса нет,
    /// его подключают так же, как тулза на рабочей машине.
    /// </summary>
    private static Task<TestDatabase> CreateDatabaseAsync() =>
        TestDatabase.CreateWithPresetAsync(extra: static services => services.AddWalletImport());

    private static WalletImportTransaction Purchase(
        string account, decimal amount, string category, string? place = null, DateOnly? on = null) =>
        new(TransactionKind.Expense, account, null, amount, category, place, on ?? new DateOnly(2025, 3, 10), RecordedAtUtc, null);

    /// <summary>
    /// Перенос записывает всё: счета в порядке файла с признаком «скрытый», место,
    /// операции всех видов — и балансы сходятся с файлом до копейки. Момент записи
    /// операции берётся из файла: по нему лента упорядочивает операции одного дня.
    /// </summary>
    [Fact]
    public async Task Перенос_записывает_счета_места_и_операции()
    {
        await using TestDatabase database = await CreateDatabaseAsync();

        WalletImportCounts written = await database.Resolve<IWalletImportHandler>().HandleAsync(Sample());

        Assert.Equal(new WalletImportCounts(2, 1, 4), written);

        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();
        Assert.Equal(["Карта", "Запас"], accounts.Select(static account => account.Name));
        Assert.Equal(100m - 30m + 1000m - 200m + 5m, accounts[0].Balance.Amount);
        Assert.Equal(200m, accounts[1].Balance.Amount);
        Assert.False(accounts[0].ExcludedFromTotals);
        Assert.True(accounts[1].ExcludedFromTotals);

        PlaceListItem place = Assert.Single(await database.Resolve<IPlacesQuery>().ReadAsync());
        Assert.Equal("Пятёрочка", place.Name);
        Assert.Equal(1, place.TransactionCount);

        await using FinanceDbContext context = await database.Resolve<IDbContextFactory<FinanceDbContext>>().CreateDbContextAsync();
        Assert.All(await context.Transactions.ToListAsync(), static row => Assert.Equal(RecordedAtUtc, row.CreatedAtUtc));
        Assert.Equal(
            "Wallet: Еда · Ашан",
            await context.Transactions.Where(static row => row.Note != null).Select(static row => row.Note).SingleAsync());
    }

    /// <summary>
    /// Второй перенос поверх первого удвоил бы все балансы: в базу, где уже есть
    /// счёт, перенос не идёт.
    /// </summary>
    [Fact]
    public async Task Перенос_идёт_только_в_пустую_базу()
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        await SaveAsync(database, Command("Наличные"));

        IWalletImportHandler handler = database.Resolve<IWalletImportHandler>();

        Assert.False(await handler.IsAvailableAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Sample()));
        Assert.Single(await database.Resolve<IAccountsQuery>().ReadAsync());
    }

    /// <summary>
    /// Перенос проходит доменные правила, как форма: нарушение на любой записи
    /// останавливает перенос до записи в базу, и его можно повторить исправленным файлом.
    /// </summary>
    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionNotBeforeAccountOpened))]
    public async Task Нарушенное_правило_останавливает_перенос()
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        IWalletImportHandler handler = database.Resolve<IWalletImportHandler>();

        DomainException error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            Sample(Purchase("Карта", 10m, "food.coffee", on: new DateOnly(2024, 12, 31)))));

        Assert.Equal(Invariant.TransactionNotBeforeAccountOpened, error.Invariant);
        Assert.True(await handler.IsAvailableAsync());
    }

    /// <summary>
    /// Сбой самой записи — уже после того, как счета и места ушли в базу, — откатывает
    /// перенос целиком. Обрыв ставится триггером на вставку операций: правила домена
    /// к этому моменту пройдены, и остановить запись может только база.
    /// </summary>
    [Fact]
    public async Task Сбой_записи_откатывает_перенос_целиком()
    {
        await using TestDatabase database = await CreateDatabaseAsync();

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER interrupt BEFORE INSERT ON transactions BEGIN SELECT RAISE(ABORT, 'interrupted'); END;");
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Resolve<IWalletImportHandler>().HandleAsync(Sample()));

        await using FinanceDbContext after = await database.Contexts.CreateDbContextAsync();
        Assert.Equal(0, await after.Accounts.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await after.Places.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>
    /// Одноимённые счета отвергаются тем же правилом, что в форме счёта: операции
    /// файла ссылаются на счёт именем, и ссылка стала бы неоднозначной.
    /// </summary>
    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameUnique))]
    public async Task Одноимённые_счета_отвергаются()
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        WalletImportFile file = Sample() with
        {
            Accounts = [.. Sample().Accounts, new("карта", AccountType.Cash, Currency.RUB, 0m, new DateOnly(2025, 1, 1), false)],
        };

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => database.Resolve<IWalletImportHandler>().HandleAsync(file));

        Assert.Equal(Invariant.NameUnique, error.Invariant);
    }

    /// <summary>
    /// У перевода в файле одна сумма, и между счетами разных валют её пришлось бы
    /// пересчитать один к одному — такой перевод останавливает перенос.
    /// </summary>
    [Fact]
    public async Task Перевод_между_валютами_останавливает_перенос()
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        WalletImportFile file = Sample(
            new WalletImportTransaction(TransactionKind.Transfer, "Карта", "Доллары", 10m, null, null, new DateOnly(2025, 3, 10), RecordedAtUtc, null));
        file = file with
        {
            Accounts = [.. file.Accounts, new("Доллары", AccountType.Cash, Currency.USD, 0m, new DateOnly(2025, 1, 1), false)],
        };

        IWalletImportHandler handler = database.Resolve<IWalletImportHandler>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(file));
        Assert.True(await handler.IsAvailableAsync());
    }

    /// <summary>
    /// Удалённая запись тоже занимает базу: перенос в базу, где счёт заводили
    /// и удалили, уже не первый запуск.
    /// </summary>
    [Fact]
    public async Task Удалённая_запись_тоже_занимает_базу()
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        await SaveAsync(database, Command("Наличные"));

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            await context.Accounts.ExecuteUpdateAsync(static rows => rows.SetProperty(static row => row.DeletedAtUtc, NowUtc));
        }

        Assert.Empty(await database.Resolve<IAccountsQuery>().ReadAsync());
        Assert.False(await database.Resolve<IWalletImportHandler>().IsAvailableAsync());
    }

    /// <summary>
    /// Перенос публикует изменение счетов, мест и операций, как любая команда
    /// через единицу работы: он пишет тем же путём, что и формы.
    /// </summary>
    [Fact]
    public async Task Перенос_оповещает_экраны()
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        IChangeNotifier notifier = database.Resolve<IChangeNotifier>();
        long accounts = notifier.VersionOf(DataChange.Accounts);
        long places = notifier.VersionOf(DataChange.Places);
        long transactions = notifier.VersionOf(DataChange.Transactions);

        await database.Resolve<IWalletImportHandler>().HandleAsync(Sample());

        Assert.True(notifier.VersionOf(DataChange.Accounts) > accounts);
        Assert.True(notifier.VersionOf(DataChange.Places) > places);
        Assert.True(notifier.VersionOf(DataChange.Transactions) > transactions);
    }

    /// <summary>
    /// Ссылка на то, чего нет — подкатегорию вне стартового набора, счёт или место
    /// вне файла, — останавливает перенос, а не пропускает запись: пропуск молча
    /// разошёлся бы с балансом Wallet.
    /// </summary>
    /// <param name="account">Счёт операции.</param>
    /// <param name="category">Подкатегория операции.</param>
    /// <param name="place">Место операции.</param>
    [Theory]
    [InlineData("Карта", "food.caviar", null)]
    [InlineData("Сейф", "food.coffee", null)]
    [InlineData("Карта", "food.coffee", "Ашан")]
    public async Task Ссылка_мимо_файла_и_набора_останавливает_перенос(string account, string category, string? place)
    {
        await using TestDatabase database = await CreateDatabaseAsync();
        IWalletImportHandler handler = database.Resolve<IWalletImportHandler>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            Sample(Purchase(account, 10m, category, place))));

        Assert.True(await handler.IsAvailableAsync());
    }

    /// <summary>
    /// Файл, записанный тулзой, читается переносом без потерь: формат задаёт
    /// один тип на обе стороны.
    /// </summary>
    [Fact]
    public async Task Файл_читается_так_же_как_записан()
    {
        string written = await WriteAsync(Sample());

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(written));
        WalletImportFile read = await WalletImportFile.ReadAsync(stream);

        Assert.Equal(written, await WriteAsync(read));
        Assert.Equal(new WalletImportCounts(2, 1, 4), WalletImportCounts.Of(read));
    }

    /// <summary>
    /// Разбор строг: чужая метка формата, другая версия, незнакомое и пропущенное
    /// поле — отказ, а не значения по умолчанию, которые записались бы в базу.
    /// </summary>
    /// <param name="original">Фрагмент настоящего файла.</param>
    /// <param name="spoiled">Чем он заменяется.</param>
    [Theory]
    [InlineData("\"format\": \"finance.wallet-import\"", "\"format\": \"finance.backup\"")]
    [InlineData("\"version\": 1", "\"version\": 2")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"source\": \"wallet\",")]
    [InlineData("\"openedOn\": \"2025-01-01\",", "")]
    [InlineData("\"kind\": \"Expense\"", "\"kind\": \"Expence\"")]
    [InlineData("\"type\": \"Card\"", "\"type\": 2")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"version\": 1,")]
    public async Task Испорченный_файл_не_разбирается(string original, string spoiled)
    {
        string json = await WriteAsync(Sample());
        string broken = json.Replace(original, spoiled, StringComparison.Ordinal);

        Assert.NotEqual(json, broken);

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(broken));
        await Assert.ThrowsAsync<JsonException>(() => WalletImportFile.ReadAsync(stream));
    }

    /// <summary>
    /// То, чего разбор сам не ловит, отвергает проверка после него: пустой элемент
    /// списка, <c>Unknown</c> в перечислении, момент записи не в UTC, имя с пробелом
    /// по краям — операция на него иначе не нашла бы счёт, записанный обрезанным.
    /// </summary>
    [Fact]
    public async Task Значения_мимо_разбора_отвергаются()
    {
        WalletImportFile sample = Sample();
        WalletImportTransaction first = sample.Transactions[0];
        WalletImportFile[] broken =
        [
            sample with { Accounts = [.. sample.Accounts, null!] },
            sample with { Transactions = [.. sample.Transactions, null!] },
            sample with { Accounts = [sample.Accounts[0] with { Currency = Currency.Unknown }, sample.Accounts[1]] },
            sample with { Transactions = [first with { Kind = TransactionKind.Unknown }] },
            sample with { Transactions = [first with { CreatedAtUtc = RecordedAtUtc.ToOffset(TimeSpan.FromHours(3)) }] },
            sample with { Places = ["Пятёрочка "] },
            sample with { Transactions = [first with { SourceAccount = " Карта" }] },
        ];

        foreach (WalletImportFile file in broken)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(await WriteAsync(file)));
            await Assert.ThrowsAsync<JsonException>(() => WalletImportFile.ReadAsync(stream));
        }
    }

    /// <summary>
    /// Клиенту перенос попадает готовой базой через восстановление из файла: база,
    /// собранная на рабочей машине, проходит проверки восстановления на свежей
    /// установке, заменяет её без предупреждения и приезжает целиком.
    /// </summary>
    [Fact]
    public async Task База_с_переносом_принимается_восстановлением()
    {
        await using TestDatabase source = await CreateDatabaseAsync();
        await source.Resolve<IWalletImportHandler>().HandleAsync(Sample());
        string file = await source.Resolve<IExportHandler>().HandleAsync();

        await using TestDatabase target = await TestDatabase.CreateWithPresetAsync();
        IRecoveryHandler recovery = target.Resolve<IRecoveryHandler>();
        RecoveryCheck check;

        await using (FileStream stream = File.OpenRead(file))
        {
            check = await recovery.CheckAsync(stream);
        }

        // Вердикт — до записи: отвергнутый файл иначе назвал бы себя лишь отказом записи
        Assert.Equal(RecoveryVerdict.Ready, check.Verdict);
        Assert.Equal(new RecoverySide { Accounts = 0, Transactions = 0, Categories = 0, Places = 0 }, check.Current);

        await recovery.RecoverAsync();

        Assert.Equal(await DumpAsync(source.Location.ConnectionString), await DumpAsync(target.Location.ConnectionString));
    }

    private static async Task<string> WriteAsync(WalletImportFile file)
    {
        using MemoryStream stream = new();
        await file.WriteAsync(stream);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
