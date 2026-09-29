using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Categories.Card;
using Finance.Application.Features.Places.Card;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Tests;

/// <summary>
/// Уход с формы: экран спрашивает про набранное только когда терять есть что.
/// Признак считается сравнением со снимком на момент загрузки — иначе подстановка
/// счёта и значка при открытии сама выглядела бы правкой.
/// </summary>
public sealed class UnsavedFormTests
{
    /// <summary>
    /// Только что открытая форма операции ничего не потеряла.
    /// </summary>
    [Fact]
    public async Task Открытая_форма_операции_не_считается_правленой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await given.AccountAsync("Карта");

        TransactionViewModel model = Transaction(given);
        await model.LoadAsync(key: null);

        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Вид, пришедший с ярлыка на значке, стоит в форме и не делает её правленой:
    /// пользователь к ней не притронулся, и вопрос на выходе был бы о чужом выборе.
    /// </summary>
    [Theory]
    [InlineData(TransactionKind.Income)]
    [InlineData(TransactionKind.Transfer)]
    public async Task Вид_с_ярлыка_подставлен_и_не_считается_правкой(TransactionKind kind)
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await given.AccountAsync("Карта");
        await given.AccountAsync("Наличные");

        TransactionViewModel model = Transaction(given);
        await model.LoadAsync(key: null, kind: kind);

        Assert.Equal(kind, model.Kind);
        Assert.False(model.IsDirty);

        // Списки под видом уже пересобраны: у перевода категорий нет,
        // у дохода предлагаются только доходные
        Assert.Equal(kind is TransactionKind.Transfer, model.Categories.Count is 0);
    }

    /// <summary>
    /// Набранная сумма — потеря: ради неё диалог и заведён.
    /// </summary>
    [Fact]
    public async Task Набранная_сумма_делает_форму_правленой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await given.AccountAsync("Карта");

        TransactionViewModel model = Transaction(given);
        await model.LoadAsync(key: null);

        model.PressKey("5");

        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Заметка и место теряются так же, как сумма.
    /// </summary>
    [Fact]
    public async Task Заметка_делает_форму_правленой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await given.AccountAsync("Карта");

        TransactionViewModel model = Transaction(given);
        await model.LoadAsync(key: null);

        model.Note = "Вернуть долг";

        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Открытая на правку операция тоже не считается правленой: загрузка расставила
    /// счёт, категорию и сумму, но это прочитанное, а не набранное.
    /// </summary>
    [Fact]
    public async Task Открытая_на_правку_операция_не_считается_правленой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid account = await given.AccountAsync("Карта", 1000m);
        Guid key = await given.SaveAsync(given.Expense(account, 250m, place: "Пятёрочка"));

        TransactionViewModel model = Transaction(given);
        await model.LoadAsync(key);

        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Карточка счёта: подставленные поля правкой не считаются, набранное — считается.
    /// </summary>
    [Fact]
    public async Task Правка_названия_счёта_делает_форму_правленой()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        AccountViewModel model = new(
            database.Resolve<IAccountCardQuery>(),
            database.Resolve<ISaveAccountHandler>(),
            database.Resolve<IClock>());

        await model.LoadAsync(key: null);

        Assert.False(model.IsDirty);

        model.Name = "Наличные";

        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Карточка места: переименование теряется, открытие — нет.
    /// </summary>
    [Fact]
    public async Task Переименование_места_делает_форму_правленой()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid account = await given.AccountAsync("Карта", 1000m);
        await given.SaveAsync(given.Expense(account, 100m, place: "Пятёрочка"));

        PlaceViewModel model = new(
            given.Database.Resolve<IPlacesQuery>(),
            given.Database.Resolve<IRenamePlaceHandler>(),
            given.Database.Resolve<IDeletePlaceHandler>());

        Guid key = (await given.Database.Resolve<IPlacesQuery>().ReadAsync()).Single().Key;

        await model.LoadAsync(key);

        Assert.False(model.IsDirty);

        model.Name = "Пятёрочка на углу";

        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Карточка группы: выбранный значок теряется так же, как название.
    /// </summary>
    [Fact]
    public async Task Выбранный_значок_группы_делает_форму_правленой()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        GroupViewModel model = new(
            database.Resolve<ICategoriesQuery>(),
            database.Resolve<ISaveCategoryHandler>(),
            database.Resolve<IconCatalog>());

        await model.LoadAsync(key: null);

        Assert.False(model.IsDirty);

        model.Icon.Pick(model.Icon.Choices[^1].Key);

        Assert.True(model.IsDirty);
    }

    private static TransactionViewModel Transaction(TransactionFixture fixture) =>
        fixture.Database.Resolve<TransactionViewModel>();
}
