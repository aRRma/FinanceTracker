using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Accounts.Catalog;
using Finance.Application.Features.More;
using Finance.Application.Features.Settings.DefaultAccounts;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Счёт по умолчанию: что подставляет форма новой операции, кто занимает место ушедшего
/// счёта по умолчанию и что об этом говорят подтверждения карточки счёта.
/// </summary>
public sealed class DefaultAccountTests
{
    /// <summary>
    /// На чистой установке выбирать нечего: первый заведённый счёт и есть счёт по умолчанию,
    /// а строки настройки при этом не появляется — её пишет только явный выбор.
    /// </summary>
    [Fact]
    public async Task Первый_заведённый_счёт_становится_счётом_по_умолчанию()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        await given.AccountAsync("Карта");

        Assert.Equal(cash, (await NewFormAsync(given)).SourceAccount?.Key);
        Assert.Null(await ChoiceAsync(given));
    }

    /// <summary>
    /// Подстановка больше не следует за операциями: запись по другому счёту её не меняет.
    /// </summary>
    [Fact]
    public async Task Операция_по_другому_счёту_не_меняет_подстановку()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Expense(card, 10m));

        Assert.Equal(cash, (await NewFormAsync(given)).SourceAccount?.Key);
    }

    /// <summary>
    /// Выбранный в настройках счёт подставляется в форму, а не верхний в списке.
    /// </summary>
    [Fact]
    public async Task Выбранный_счёт_подставляется_в_форму()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");

        await ChooseAsync(given, card);

        Assert.Equal(card, (await NewFormAsync(given)).SourceAccount?.Key);
    }

    /// <summary>
    /// Форма из ленты счёта подставляет этот счёт, а не счёт по умолчанию.
    /// </summary>
    [Fact]
    public async Task Лента_счёта_сильнее_счёта_по_умолчанию()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");

        Assert.Equal(card, (await NewFormAsync(given, fromFeed: card)).SourceAccount?.Key);
    }

    /// <summary>
    /// Из ленты заблокированного счёта форма подставляет счёт по умолчанию:
    /// заблокированный привёл бы к отказу при сохранении.
    /// </summary>
    [Fact]
    public async Task Лента_заблокированного_счёта_уступает_счёту_по_умолчанию()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        Guid card = await given.AccountAsync("Карта");
        await given.CloseAsync(card);

        Assert.Equal(cash, (await NewFormAsync(given, fromFeed: card)).SourceAccount?.Key);
    }

    /// <summary>
    /// Заблокированный счёт по умолчанию уступает место верхнему, и выбор забывается:
    /// после разблокировки он роль себе не возвращает.
    /// </summary>
    [Fact]
    public async Task Заблокированный_счёт_по_умолчанию_уступает_верхнему_и_не_возвращается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        await given.AccountAsync("Второй");
        Guid third = await given.AccountAsync("Третий");

        await ChooseAsync(given, third);
        await given.CloseAsync(third);

        Assert.Equal(first, (await NewFormAsync(given)).SourceAccount?.Key);
        Assert.Null(await ChoiceAsync(given));

        await ReopenAsync(given, third);

        Assert.Equal(first, (await NewFormAsync(given)).SourceAccount?.Key);
    }

    /// <summary>
    /// Удалённый счёт по умолчанию забывается так же, как заблокированный.
    /// </summary>
    [Fact]
    public async Task Удалённый_счёт_по_умолчанию_забывается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");

        await ChooseAsync(given, second);
        await given.Database.Resolve<IDeleteAccountHandler>().HandleAsync(second);

        Assert.Equal(first, (await NewFormAsync(given)).SourceAccount?.Key);
        Assert.Null(await ChoiceAsync(given));
    }

    /// <summary>
    /// Блокировка другого счёта выбор не трогает.
    /// </summary>
    [Fact]
    public async Task Блокировка_другого_счёта_не_трогает_выбор()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");

        await ChooseAsync(given, second);
        await given.CloseAsync(first);

        Assert.Equal(second, (await NewFormAsync(given)).SourceAccount?.Key);
        Assert.Equal(second.ToString(), await ChoiceAsync(given));
    }

    /// <summary>
    /// Пока выбора нет, счёт по умолчанию — верхний в списке, и перестановка списка его меняет.
    /// </summary>
    [Fact]
    public async Task Без_выбора_счёт_по_умолчанию_следует_за_порядком_списка()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");

        await given.Database.Resolve<IReorderAccountsHandler>().HandleAsync([second, first]);

        Assert.Equal(second, (await NewFormAsync(given)).SourceAccount?.Key);
    }

    /// <summary>
    /// Испорченное значение настройки равно отсутствию выбора, а не ошибке на каждом открытии формы.
    /// </summary>
    [Fact]
    public async Task Испорченная_настройка_равна_отсутствию_выбора()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        await given.AccountAsync("Второй");

        await given.Database.Resolve<ILocalSettings>().SetAsync(SettingName.DefaultAccountKey, "not-a-key");

        Assert.Equal(first, (await NewFormAsync(given)).SourceAccount?.Key);
    }

    /// <summary>
    /// Незаблокированных счетов нет — подставлять нечего, и «Ещё» так и говорит.
    /// </summary>
    [Fact]
    public async Task Без_незаблокированных_счетов_подставлять_нечего()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid cash = await given.AccountAsync("Наличные");
        await given.CloseAsync(cash);

        TransactionViewModel form = await NewFormAsync(given);

        Assert.Null(form.SourceAccount);
        Assert.True(form.NeedsNewAccount);

        MoreViewModel more = given.Database.Resolve<MoreViewModel>();
        await more.LoadAsync();

        Assert.Equal(UiTexts.DefaultAccountEmptyTitle, more.DefaultAccountCaption);
    }

    /// <summary>
    /// Блокировка счёта по умолчанию называет в подтверждении того, кто займёт его место.
    /// </summary>
    [Fact]
    public async Task Блокировка_счёта_по_умолчанию_называет_преемника()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        await given.AccountAsync("Второй");

        AccountViewModel model = await CardAsync(given, first);
        model.IsClosed = true;

        Assert.Equal(Moves("Второй"), model.ClosingWarning);
    }

    /// <summary>
    /// Последний незаблокированный счёт уступать некому — подтверждение так и говорит.
    /// </summary>
    [Fact]
    public async Task Блокировка_последнего_счёта_говорит_что_счёта_по_умолчанию_не_останется()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid only = await given.AccountAsync("Единственный");

        AccountViewModel model = await CardAsync(given, only);
        model.IsClosed = true;

        Assert.Equal(UiTexts.AccountDefaultGone, model.ClosingWarning);
    }

    /// <summary>
    /// Пустой счёт не по умолчанию блокируется и удаляется без последствий — и без текста в подтверждении.
    /// </summary>
    [Fact]
    public async Task Счёт_не_по_умолчанию_уходит_без_пояснений()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");

        AccountViewModel model = await CardAsync(given, second);
        model.IsClosed = true;

        Assert.Null(model.ClosingWarning);
        Assert.Null(model.DeleteNote);
    }

    /// <summary>
    /// Деньги на счёте и смена подстановки — два последствия одного вопроса, каждое своей строкой.
    /// </summary>
    [Fact]
    public async Task Остаток_и_преемник_в_одном_предупреждении()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый", 100m);
        await given.AccountAsync("Второй");

        AccountViewModel model = await CardAsync(given, first);
        model.IsClosed = true;

        string money = string.Format(UiCulture.Current, UiTexts.AccountClosingWarning, Money.Create(100m, Currency.RUB).Display);

        Assert.Equal($"{money}\n{Moves("Второй")}", model.ClosingWarning);
    }

    /// <summary>
    /// Удаление счёта по умолчанию называет преемника в тексте подтверждения.
    /// </summary>
    [Fact]
    public async Task Удаление_счёта_по_умолчанию_называет_преемника()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");

        await ChooseAsync(given, second);

        AccountViewModel model = await CardAsync(given, second);

        Assert.Equal(Moves("Первый"), model.DeleteNote);
    }

    /// <summary>
    /// Экран выбора показывает только незаблокированные счета, галочка — у счёта по умолчанию
    /// и переезжает по касанию; подпись в «Ещё» называет выбранный.
    /// </summary>
    [Fact]
    public async Task Экран_выбора_отмечает_счёт_по_умолчанию_и_меняет_его()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");
        Guid closed = await given.AccountAsync("Старый");
        await given.CloseAsync(closed);

        DefaultAccountViewModel screen = given.Database.Resolve<DefaultAccountViewModel>();
        await screen.LoadAsync();

        Assert.Equal([first, second], screen.Accounts.Select(static option => option.Key));
        Assert.Equal(first, Assert.Single(screen.Accounts, static option => option.IsSelected).Key);
        Assert.False(screen.IsEmpty);

        await screen.SelectAsync(screen.Accounts[1]);

        Assert.Equal(second, Assert.Single(screen.Accounts, static option => option.IsSelected).Key);
        Assert.Equal(second, (await NewFormAsync(given)).SourceAccount?.Key);

        MoreViewModel more = given.Database.Resolve<MoreViewModel>();
        await more.LoadAsync();

        Assert.Equal("Второй", more.DefaultAccountCaption);
    }

    /// <summary>
    /// Касание по счёту, заблокированному, пока экран был открыт, выбор не пишет —
    /// иначе он ожил бы при разблокировке, — а экран перечитывается без этой строки.
    /// </summary>
    [Fact]
    public async Task Устаревший_список_не_записывает_заблокированный_счёт()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid first = await given.AccountAsync("Первый");
        Guid second = await given.AccountAsync("Второй");

        DefaultAccountViewModel screen = given.Database.Resolve<DefaultAccountViewModel>();
        await screen.LoadAsync();

        DefaultAccountOption stale = screen.Accounts.Single(option => option.Key == second);
        await given.CloseAsync(second);

        await screen.SelectAsync(stale);

        Assert.Null(await ChoiceAsync(given));
        Assert.Equal([first], screen.Accounts.Select(static option => option.Key));
        Assert.True(Assert.Single(screen.Accounts).IsSelected);

        await ReopenAsync(given, second);

        Assert.Equal(first, (await NewFormAsync(given)).SourceAccount?.Key);
    }

    /// <summary>
    /// Без незаблокированных счетов экран выбора пуст и говорит об этом.
    /// </summary>
    [Fact]
    public async Task Экран_выбора_без_счетов_пуст()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        DefaultAccountViewModel screen = given.Database.Resolve<DefaultAccountViewModel>();
        Assert.False(screen.IsEmpty);

        await screen.LoadAsync();

        Assert.Empty(screen.Accounts);
        Assert.True(screen.IsEmpty);
    }

    /// <summary>
    /// Установка, обновлённая с последним использованным счётом, его строку теряет,
    /// а прочие настройки остаются.
    /// </summary>
    [Fact]
    public async Task Миграция_забывает_последний_использованный_счёт()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();
        await database.MigrateToAsync("AddAccountColorAndIcon");

        await database.ExecuteAsync(
            $"""
            INSERT INTO settings (name, value) VALUES ('transactions.last_account_key', '{Keys.New()}');
            INSERT INTO settings (name, value) VALUES ('{SettingName.Theme}', 'Dark');
            """);

        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        ILocalSettings settings = database.Resolve<ILocalSettings>();

        Assert.Null(await settings.GetAsync("transactions.last_account_key"));
        Assert.Equal("Dark", await settings.GetAsync(SettingName.Theme));
    }

    private static string Moves(string successor) =>
        string.Format(UiCulture.Current, UiTexts.AccountDefaultMoves, successor);

    private static Task ChooseAsync(TransactionFixture given, Guid account) =>
        given.Database.Resolve<IChangeDefaultAccountHandler>().HandleAsync(account);

    private static Task<string?> ChoiceAsync(TransactionFixture given) =>
        given.Database.Resolve<ILocalSettings>().GetAsync(SettingName.DefaultAccountKey);

    private static async Task<TransactionViewModel> NewFormAsync(TransactionFixture given, Guid? fromFeed = null)
    {
        TransactionViewModel form = given.Database.Resolve<TransactionViewModel>();
        await form.LoadAsync(key: null, accountKey: fromFeed);

        return form;
    }

    private static async Task<AccountViewModel> CardAsync(TransactionFixture given, Guid account)
    {
        AccountViewModel model = given.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(account);

        return model;
    }

    private static async Task ReopenAsync(TransactionFixture given, Guid account)
    {
        AccountViewModel model = await CardAsync(given, account);
        model.IsClosed = false;

        Assert.True(await model.SaveAsync(), model.Error);
    }
}
