using System.Text;
using System.Text.Json;
using Finance.Application.Features.WalletImport;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Microsoft.EntityFrameworkCore;

using static Finance.Application.Tests.AccountSetup;

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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

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
    /// счёт, перенос не идёт, и строка переноса там не показывается.
    /// </summary>
    [Fact]
    public async Task Перенос_идёт_только_в_пустую_базу()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
        await SaveAsync(database, Command("Наличные"));

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            await context.Accounts.ExecuteUpdateAsync(static rows => rows.SetProperty(static row => row.DeletedAtUtc, NowUtc));
        }

        Assert.Empty(await database.Resolve<IAccountsQuery>().ReadAsync());
        Assert.False(await database.Resolve<IWalletImportHandler>().IsAvailableAsync());
    }

    /// <summary>
    /// После переноса экраны узнают о новых счетах, местах и операциях: балансы
    /// и лента, открытые до переноса, перечитаются на возврате.
    /// </summary>
    [Fact]
    public async Task Перенос_оповещает_экраны()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
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
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
        IWalletImportHandler handler = database.Resolve<IWalletImportHandler>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            Sample(Purchase(account, 10m, category, place))));

        Assert.True(await handler.IsAvailableAsync());
    }

    /// <summary>
    /// Файл, записанный тулзой, читается приложением без потерь: формат задаёт
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
    /// Сбой записи снимает признак ожидания: иначе строка крутилась бы вечно,
    /// а повторить перенос было бы нечем.
    /// </summary>
    [Fact]
    public async Task Сбой_переноса_снимает_ожидание()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
        WalletImportViewModel model = database.Resolve<WalletImportViewModel>();

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(await WriteAsync(Sample())));
        Assert.True(await model.ReadAsync(stream));
        await SaveAsync(database, Command("Наличные"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => model.ImportAsync());

        Assert.False(model.IsImporting);
        Assert.Single(await database.Resolve<IAccountsQuery>().ReadAsync());
    }

    /// <summary>
    /// Строка переноса видна на пустой базе, перед записью называет числа из файла,
    /// а после переноса пропадает.
    /// </summary>
    [Fact]
    public async Task Строка_переноса_спрашивает_с_числами_и_пропадает_после_переноса()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
        WalletImportViewModel model = database.Resolve<WalletImportViewModel>();

        await model.LoadAsync();
        Assert.True(model.IsAvailable);

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(await WriteAsync(Sample())));
        Assert.True(await model.ReadAsync(stream));
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.WalletImportConfirmText, 2, 1, 4), model.ConfirmText);

        string done = await model.ImportAsync();

        Assert.Equal(string.Format(UiCulture.Current, UiTexts.WalletImportDoneText, 2, 1, 4), done);
        Assert.False(model.IsAvailable);
        Assert.False(model.IsImporting);
    }

    /// <summary>
    /// Выбран не тот файл — это не сбой приложения: модель отвечает отказом,
    /// и страница говорит об этом своими словами.
    /// </summary>
    [Fact]
    public async Task Чужой_файл_получает_отказ_а_не_сбой()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();
        WalletImportViewModel model = database.Resolve<WalletImportViewModel>();

        using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"theme\": \"dark\"}"));

        Assert.False(await model.ReadAsync(stream));
        Assert.Empty(model.ConfirmText);
    }

    private static async Task<string> WriteAsync(WalletImportFile file)
    {
        using MemoryStream stream = new();
        await file.WriteAsync(stream);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
