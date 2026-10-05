using Finance.Application.Features.Accounts.Badge;
using Finance.Application.Features.Accounts.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Цвет и значок счёта: очередь цвета у нового счёта, экран выбора и возврат
/// выбора в карточку, знак счёта в ленте.
/// </summary>
public sealed class AccountBadgeTests
{
    /// <summary>
    /// Новый счёт получает первый свободный цвет ещё до набора, и нетронутая
    /// карточка правленой не считается: цвет подставлен до снимка.
    /// </summary>
    [Fact]
    public async Task Новый_счёт_получает_свободный_цвет_до_набора()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Основная");

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key: null);

        Assert.Equal(AccountColor.Orange, model.Color);
        Assert.False(model.IsDirty);

        model.Name = "Вторая";
        Assert.True(await model.SaveAsync(), model.Error);

        Assert.Equal(AccountColor.Orange, (await AccountAsync(fixture, "Вторая")).Color);
    }

    /// <summary>
    /// Заблокированный счёт цвет не занимает: новый получает его цвет.
    /// </summary>
    [Fact]
    public async Task Цвет_заблокированного_свободен()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid old = await fixture.AccountAsync("Старая");
        await fixture.CloseAsync(old);

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key: null);

        Assert.Equal(AccountColor.Blue, model.Color);
    }

    /// <summary>
    /// Значок по типу следует за сменой типа, пока его не выбрали руками; выбранный остаётся.
    /// </summary>
    [Fact]
    public async Task Значок_следует_за_типом_пока_не_выбран()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key: null);
        model.Name = "Кошелёк";

        model.Type = AccountType.Cash;
        Assert.Equal("cash", model.Mark.Icon);

        await PickAsync(fixture, model, static screen => screen.PickIcon(screen.Icons.Single(static icon => icon.Key == "wallet")));

        model.Type = AccountType.Card;
        Assert.Equal("wallet", model.Mark.Icon);
    }

    /// <summary>
    /// Выбор на экране уходит в карточку один раз, делает её правленой и сохраняется
    /// только вместе с ней.
    /// </summary>
    [Fact]
    public async Task Выбор_возвращается_в_карточку_и_сохраняется_с_ней()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid key = await fixture.AccountAsync("Отпуск");

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);

        await PickAsync(fixture, model, static screen =>
        {
            screen.PickColor(screen.Colors.Single(static color => color.Color is AccountColor.Green));
            screen.PickIcon(screen.Icons.Single(static icon => icon.Key == "plane"));
        });

        Assert.Equal(AccountColor.Green, model.Color);
        Assert.Equal("plane", model.Icon);
        Assert.True(model.IsDirty);

        // До сохранения в базе прежнее: экран выбора — часть формы
        Assert.Equal(AccountColor.Blue, (await AccountAsync(fixture, "Отпуск")).Color);

        Assert.True(await model.SaveAsync(), model.Error);

        AccountListItem saved = await AccountAsync(fixture, "Отпуск");
        Assert.Equal(AccountColor.Green, saved.Color);
        Assert.Equal("plane", saved.Icon);
    }

    /// <summary>
    /// Ушли с экрана, ничего не тронув, — карточка остаётся нетронутой.
    /// </summary>
    [Fact]
    public async Task Без_касаний_выбор_карточку_не_правит()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid key = await fixture.AccountAsync("Основная");

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);

        await PickAsync(fixture, model, static _ => { });

        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Экран показывает набранное в карточке, отмечает текущие цвет и значок,
    /// подсказывает значки по названию и берёт последнюю операцию для предпросмотра.
    /// </summary>
    [Fact]
    public async Task Экран_выбора_показывает_счёт_как_он_набран()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid key = await fixture.AccountAsync("Карта");
        await fixture.SaveAsync(fixture.Expense(key, 250m));

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);
        model.Name = "Вклад";
        model.OpenBadge();

        AccountBadgeViewModel screen = fixture.Database.Resolve<AccountBadgeViewModel>();
        await screen.LoadAsync();

        Assert.Equal("Вклад", screen.Mark.Name);
        Assert.Equal(AccountColor.Blue, Assert.Single(screen.Colors, static color => color.IsSelected).Color);
        Assert.Equal("credit-card", Assert.Single(screen.Icons, static icon => icon.IsSelected).Key);
        Assert.Equal(["building-bank", "coins"], screen.Hints.Select(static hint => hint.Key));
        Assert.Equal(AccountColors.Queue.Count, screen.Colors.Count);
        Assert.Equal(AccountIcon.Choices.Count, screen.Icons.Count);

        AccountBadgeFeedRow row = Assert.IsType<AccountBadgeFeedRow>(screen.FeedRow);
        Assert.True(row.IsExpense);

        // Касание перекрашивает и предпросмотр ленты
        screen.PickColor(screen.Colors.Single(static color => color.Color is AccountColor.Violet));
        Assert.Equal(AccountColor.Violet, screen.FeedRow!.Account.Color);
    }

    /// <summary>
    /// Последний перевод в предпросмотре — как в общей ленте: от списания к зачислению,
    /// оба счёта жетонами, и перекрашивается знак именно этого счёта.
    /// </summary>
    [Fact]
    public async Task Перевод_в_предпросмотре_идёт_от_списания_к_зачислению()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid card = await fixture.AccountAsync("Карта");
        Guid savings = await fixture.AccountAsync("Копилка");
        await fixture.SaveAsync(fixture.Transfer(card, savings, 100m));

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(savings);
        model.OpenBadge();

        AccountBadgeViewModel screen = fixture.Database.Resolve<AccountBadgeViewModel>();
        await screen.LoadAsync();
        screen.PickColor(screen.Colors.Single(static color => color.Color is AccountColor.Red));

        AccountBadgeFeedRow row = Assert.IsType<AccountBadgeFeedRow>(screen.FeedRow);
        Assert.Equal("Карта", row.CaptionAccount.Name);
        Assert.Equal(new AccountMark("Копилка", AccountColor.Red, "credit-card"), row.CaptionTargetAccount);
    }

    /// <summary>
    /// Баланс в предпросмотре — от набранного остатка: правка остатка сдвигает баланс
    /// на разницу, а операции по счёту остаются в нём.
    /// </summary>
    [Fact]
    public async Task Баланс_в_предпросмотре_от_набранного_остатка()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid key = await fixture.AccountAsync("Карта", openingBalance: 100m);
        await fixture.SaveAsync(fixture.Expense(key, 30m));

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);
        model.OpeningBalance = "5000";
        model.OpenBadge();

        AccountBadgeViewModel screen = fixture.Database.Resolve<AccountBadgeViewModel>();
        await screen.LoadAsync();

        Assert.Equal(Money.Create(4970m, Currency.RUB).Display, screen.Balance);
    }

    /// <summary>
    /// Набранное, пока новая карточка ждёт свой цвет, — правка: снимок снят с пустой формы
    /// до чтения, и уход без сохранения спросит о ней.
    /// </summary>
    [Fact]
    public async Task Набранное_пока_читается_цвет_считается_правкой()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        DelayingColor query = new(fixture.Database.Resolve<IAccountCardQuery>());
        AccountViewModel model = new(
            query,
            fixture.Database.Resolve<ISaveAccountHandler>(),
            fixture.Database.Resolve<IDeleteAccountHandler>(),
            fixture.Database.Resolve<IClock>(),
            fixture.Database.Resolve<AccountBadgeDraft>());

        Task loading = model.LoadAsync(key: null);
        model.Name = "Набрано заранее";
        query.Release();
        await loading;

        Assert.Equal(AccountColor.Blue, model.Color);
        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Новому счёту показать в ленте нечего — строки предпросмотра нет.
    /// </summary>
    [Fact]
    public async Task У_нового_счёта_предпросмотра_ленты_нет()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key: null);
        model.OpenBadge();

        AccountBadgeViewModel screen = fixture.Database.Resolve<AccountBadgeViewModel>();
        await screen.LoadAsync();

        Assert.False(screen.HasFeedPreview);
    }

    /// <summary>
    /// Лента несёт знак счёта стороны строки, а у перевода — и второго счёта.
    /// </summary>
    [Fact]
    public async Task Лента_несёт_знаки_обоих_счетов_перевода()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid card = await fixture.AccountAsync("Карта");
        Guid savings = await fixture.AccountAsync("Копилка", excluded: true);

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(savings);
        await PickAsync(fixture, model, static screen =>
            screen.PickColor(screen.Colors.Single(static color => color.Color is AccountColor.Pink)));
        Assert.True(await model.SaveAsync(), model.Error);

        await fixture.SaveAsync(fixture.Transfer(card, savings, 100m));

        FeedItem common = Assert.Single((await fixture.FeedAsync()).Items);
        Assert.Equal(new AccountMark("Карта", AccountColor.Blue, "credit-card"), common.Account);
        Assert.Equal(new AccountMark("Копилка", AccountColor.Pink, "building-bank"), common.OtherAccount);

        // В ленте копилки строка — со стороны зачисления: свой счёт и второй меняются местами
        FeedItem own = Assert.Single((await fixture.FeedAsync(savings)).Items);
        Assert.Equal(AccountColor.Pink, own.Account.Color);
        Assert.Equal(AccountColor.Blue, own.OtherAccount?.Color);
    }

    /// <summary>
    /// Команда без цвета отвергается: Unknown, записанный в базу, рисовался бы знаком без заливки.
    /// </summary>
    [Fact]
    public async Task Команда_без_цвета_отвергается()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Database.Resolve<ISaveAccountHandler>().HandleAsync(
                AccountSetup.Command("Без цвета") with { Color = AccountColor.Unknown }));
    }

    // Путь пользователя: из карточки на экран выбора, касания там, «назад» в карточку
    private static async Task PickAsync(TransactionFixture fixture, AccountViewModel model, Action<AccountBadgeViewModel> touch)
    {
        model.OpenBadge();

        AccountBadgeViewModel screen = fixture.Database.Resolve<AccountBadgeViewModel>();
        await screen.LoadAsync();
        touch(screen);

        model.ApplyBadge();
    }

    private static async Task<AccountListItem> AccountAsync(TransactionFixture fixture, string name) =>
        (await fixture.Database.Resolve<IAccountsQuery>().ReadAsync()).Single(account => account.Name == name);

    // Держит чтение цвета, пока тест не отпустит: на настоящей базе оно завершается
    // на месте, и ввод «во время чтения» не подстроить
    private sealed class DelayingColor(IAccountCardQuery inner) : IAccountCardQuery
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.SetResult();

        public Task<AccountCard?> ReadAsync(Guid key, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(key, cancellationToken);

        public async Task<AccountColor> ReadFreeColorAsync(CancellationToken cancellationToken = default)
        {
            await _gate.Task;

            return await inner.ReadFreeColorAsync(cancellationToken);
        }
    }
}
