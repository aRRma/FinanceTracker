using Finance.Application.Features.Places.Card;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Справочник мест: счётчики, переименование и удаление. Место заводится только
/// из формы операции, поэтому и здесь оно появляется через запись операции.
/// </summary>
public sealed class PlacesTests
{
    /// <summary>
    /// У места считается число операций и подкатегория, в которой оно встречается
    /// чаще всего: по этой паре дубль отличается от исходного места.
    /// </summary>
    [Fact]
    public async Task Счётчики_называют_число_операций_и_частую_подкатегорию()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));
        await fixture.SaveAsync(fixture.Expense(account, 200m, place: "Пятёрочка"));

        string categoryName = await CategoryNameAsync(fixture, fixture.ExpenseCategory);

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        Assert.Equal("Пятёрочка", place.Name);
        Assert.Equal(2, place.TransactionCount);
        Assert.Equal(categoryName, place.TopCategoryName);
        Assert.True(place.HasTopCategory);
    }

    /// <summary>
    /// Порядок задаётся частотой, а не алфавитом: справочник наполняется сам,
    /// и наверху должно оказаться то, чем пользуются.
    /// </summary>
    [Fact]
    public async Task Частые_места_идут_первыми()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Ашан"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Магнит"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Магнит"));

        IReadOnlyList<PlaceListItem> places = await fixture.Database.Resolve<IPlacesQuery>().ReadAsync();

        Assert.Equal(["Магнит", "Ашан"], places.Select(static place => place.Name));
    }

    /// <summary>
    /// При равном числе операций порядок задаёт русский алфавит, а не коды символов:
    /// строчная «ё» стоит в кодировке после «я», и порядок по кодам увёл бы «ёлки»
    /// в конец справочника.
    /// </summary>
    [Fact]
    public async Task Равные_по_частоте_места_идут_по_алфавиту()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "яблоко"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "ёлки"));

        IReadOnlyList<PlaceListItem> places = await fixture.Database.Resolve<IPlacesQuery>().ReadAsync();

        Assert.Equal(["ёлки", "яблоко"], places.Select(static place => place.Name));
    }

    /// <summary>
    /// Переименование правит все операции разом: они ссылаются на ключ, и строки
    /// операций при этом не трогаются. Ради этого место и сделано справочником.
    /// </summary>
    [Fact]
    public async Task Переименование_видно_во_всех_операциях()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятерочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());
        DateTimeOffset stampBefore = await fixture.StampAsync(transaction);

        await fixture.Database.Resolve<IRenamePlaceHandler>().HandleAsync(place.Key, "  Пятёрочка  ");

        TransactionCard card = (await fixture.Database.Resolve<ITransactionCardQuery>().ReadAsync(transaction))!;

        // Имя хранится обрезанным, а метка операции не сдвинулась: правилось место,
        // а не операция, и будущему обмену незачем видеть её изменённой
        Assert.Equal("Пятёрочка", card.PlaceName);
        Assert.Equal(stampBefore, await fixture.StampAsync(transaction));
    }

    /// <summary>
    /// Имя места уникально по всему справочнику: ни вида, ни группы у места нет.
    /// </summary>
    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameUnique))]
    public async Task Переименование_в_занятое_имя_отвергается()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Магнит"));

        IReadOnlyList<PlaceListItem> places = await fixture.Database.Resolve<IPlacesQuery>().ReadAsync();
        Guid magnet = places.Single(place => place.Name == "Магнит").Key;

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => fixture.Database.Resolve<IRenamePlaceHandler>().HandleAsync(magnet, "пятёрочка"));

        Assert.Equal(Invariant.NameUnique, error.Invariant);
    }

    /// <summary>
    /// Своё собственное имя месту не мешает: иначе оно конфликтовало бы само с собой.
    /// </summary>
    [Fact]
    public async Task Переименование_в_своё_же_имя_проходит()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        await fixture.Database.Resolve<IRenamePlaceHandler>().HandleAsync(place.Key, "Пятёрочка");

        Assert.Equal("Пятёрочка", Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync()).Name);
    }

    /// <summary>
    /// Удаление места не трогает строки операций: ссылка остаётся, просто
    /// перестаёт разрешаться, и операция показывается как операция без места.
    /// Массовая правка ссылок запрещена — она породила бы сотни строк в очереди
    /// отправки и искусственные конфликты слияния.
    /// </summary>
    [Fact]
    public async Task Удаление_места_не_трогает_операции()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());
        DateTimeOffset stampBefore = await fixture.StampAsync(transaction);

        await fixture.Database.Resolve<IDeletePlaceHandler>().HandleAsync(place.Key);

        TransactionCard card = (await fixture.Database.Resolve<ITransactionCardQuery>().ReadAsync(transaction))!;

        Assert.Null(card.PlaceName);
        Assert.Equal(stampBefore, await fixture.StampAsync(transaction));
        Assert.Equal(Money.Create(900m, Currency.RUB), await fixture.BalanceAsync(account));

        // Ссылка на месте, надгробие проставлено: физического удаления не было
        await using FinanceDbContext context = await fixture.Database.Contexts.CreateDbContextAsync();

        Assert.Equal(
            place.Key,
            await context.Transactions.Where(row => row.Key == transaction).Select(row => row.PlaceKey).SingleAsync());

        Assert.NotNull(
            await context.Places.IgnoreQueryFilters()
                .Where(row => row.Key == place.Key)
                .Select(row => row.DeletedAtUtc)
                .SingleAsync());
    }

    /// <summary>
    /// Удалённое место исчезает и из справочника, и из подсказок формы операции.
    /// </summary>
    [Fact]
    public async Task Удалённое_место_не_предлагается()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        await fixture.Database.Resolve<IDeletePlaceHandler>().HandleAsync(place.Key);

        Assert.Empty(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());
    }

    /// <summary>
    /// Удалённое имя освобождается: новая операция с тем же названием заводит
    /// новое место, а не воскрешает старое.
    /// </summary>
    [Fact]
    public async Task Имя_удалённого_места_освобождается()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));

        PlaceListItem deleted = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());
        await fixture.Database.Resolve<IDeletePlaceHandler>().HandleAsync(deleted.Key);

        await fixture.SaveAsync(fixture.Expense(account, 50m, place: "Пятёрочка"));

        PlaceListItem created = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        Assert.NotEqual(deleted.Key, created.Key);
        Assert.Equal(1, created.TransactionCount);
    }

    /// <summary>
    /// Повторное удаление — не ошибка: так выглядит нажатие по неуспевшему экрану.
    /// </summary>
    [Fact]
    public async Task Повторное_удаление_места_проходит_молча()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 100m, place: "Пятёрочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        await fixture.Database.Resolve<IDeletePlaceHandler>().HandleAsync(place.Key);
        await fixture.Database.Resolve<IDeletePlaceHandler>().HandleAsync(place.Key);

        Assert.Empty(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());
    }

    private static async Task<string> CategoryNameAsync(TransactionFixture fixture, Guid key)
    {
        await using FinanceDbContext context = await fixture.Database.Contexts.CreateDbContextAsync();

        return await context.Categories.Where(row => row.Key == key).Select(row => row.Name).SingleAsync();
    }
}
