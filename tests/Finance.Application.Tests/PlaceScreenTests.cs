using Finance.Application.Features.Places.Card;
using Finance.Application.Features.Places.Catalog;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Tests;

/// <summary>Экраны справочника мест и карточки места.</summary>
public sealed class PlaceScreenTests
{
    /// <summary>
    /// Отбор по буквам идёт по прочитанному, а не по базе: буквы набирают подряд,
    /// и каждая стоила бы запроса со счётом операций по всей ленте.
    /// </summary>
    [Fact]
    public async Task Отбор_по_буквам_не_ходит_в_базу_заново()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Магнит"));

        CountingPlaces places = new(fixture.Database.Resolve<IPlacesQuery>());
        PlacesViewModel model = new(places, fixture.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        Assert.Equal(2, model.Places.Count);
        Assert.Equal(1, places.Reads);

        model.Filter = "маг";

        Assert.Equal("Магнит", Assert.Single(model.Places).Name);
        Assert.Equal(1, places.Reads);
    }

    /// <summary>Отбор не различает регистр: набирают в спешке, а не по паспорту места.</summary>
    [Fact]
    public async Task Отбор_не_различает_регистр()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "ВкусВилл"));

        PlacesViewModel model = Catalog(fixture);
        await model.LoadAsync();

        model.Filter = "ВКУСВИЛЛ";

        Assert.Single(model.Places);
    }

    /// <summary>
    /// Пустой справочник и справочник, из которого отбор ничего не выбрал, — разные
    /// состояния: в первом подсказывают, откуда берутся места, во втором — что
    /// набранные буквы никому не подошли.
    /// </summary>
    [Fact]
    public async Task Пустой_справочник_и_пустой_отбор_различаются()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        PlacesViewModel model = Catalog(fixture);
        await model.LoadAsync();

        Assert.True(model.IsEmpty);
        Assert.False(model.IsFilteredOut);

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Магнит"));

        await model.LoadAsync();
        model.Filter = "пят";

        Assert.False(model.IsEmpty);
        Assert.True(model.IsFilteredOut);
    }

    /// <summary>Справочник перечитывается сам, когда место правят из карточки.</summary>
    [Fact]
    public async Task Справочник_перечитывается_по_изменению_мест()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятерочка"));

        PlacesViewModel model = Catalog(fixture);
        model.Activate();

        try
        {
            await model.LoadAsync();

            Guid key = Assert.Single(model.Places).Key;

            await fixture.Database.Resolve<IRenamePlaceHandler>().HandleAsync(key, "Пятёрочка");

            Assert.Equal("Пятёрочка", Assert.Single(model.Places).Name);
        }
        finally
        {
            model.Deactivate();
        }
    }

    /// <summary>Подпись карточки называет число операций и самую частую подкатегорию.</summary>
    [Fact]
    public async Task Карточка_показывает_счётчики_места()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        PlaceViewModel model = Card(fixture);
        await model.LoadAsync(place.Key);

        Assert.True(model.IsLoaded);
        Assert.Equal("Пятёрочка", model.Name);
        Assert.StartsWith("2 операции · чаще всего «", model.UsageCaption, StringComparison.Ordinal);
        Assert.Equal("Удалить «Пятёрочка»?", model.DeleteTitle);
        Assert.Equal("2 операции останутся без места. Суммы, даты и балансы не изменятся.", model.DeletePrompt);
    }

    /// <summary>
    /// Заголовок подтверждения называет записанное имя, а не набранное в поле:
    /// удаляется место как оно сохранено, и правка в поле к удалению не относится.
    /// </summary>
    [Fact]
    public async Task Подтверждение_удаления_называет_записанное_имя()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятерочка"));

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        PlaceViewModel model = Card(fixture);
        await model.LoadAsync(place.Key);

        model.Name = "Совсем другое";

        Assert.Equal("Удалить «Пятерочка»?", model.DeleteTitle);
    }

    /// <summary>Неиспользованное место удаляется без разговоров о переезде операций.</summary>
    [Fact]
    public async Task Подтверждение_для_места_без_операций_не_считает_операции()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));

        await fixture.Database.Resolve<IDeleteTransactionHandler>().HandleAsync(transaction);

        PlaceListItem place = Assert.Single(await fixture.Database.Resolve<IPlacesQuery>().ReadAsync());

        PlaceViewModel model = Card(fixture);
        await model.LoadAsync(place.Key);

        Assert.Equal(0, place.TransactionCount);
        Assert.Equal("Место ещё не использовано ни в одной операции", model.UsageCaption);
        Assert.Equal("Операций с этим местом нет. Отменить удаление будет нельзя.", model.DeletePrompt);
    }

    /// <summary>Занятое имя не сохраняется, а показывается на форме — экран остаётся открытым.</summary>
    [Fact]
    public async Task Занятое_имя_показывается_ошибкой_и_не_закрывает_карточку()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);

        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Пятёрочка"));
        await fixture.SaveAsync(fixture.Expense(account, 10m, place: "Магнит"));

        IReadOnlyList<PlaceListItem> places = await fixture.Database.Resolve<IPlacesQuery>().ReadAsync();

        PlaceViewModel model = Card(fixture);
        await model.LoadAsync(places.Single(place => place.Name == "Магнит").Key);

        model.Name = "Пятёрочка";

        Assert.False(await model.SaveAsync());
        Assert.True(model.HasError);
        Assert.Contains("занято", model.Error, StringComparison.Ordinal);
    }

    /// <summary>Карточка несуществующего места не загружается: править и удалять нечего.</summary>
    [Fact]
    public async Task Карточка_неизвестного_места_остаётся_пустой()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        PlaceViewModel model = Card(fixture);
        await model.LoadAsync(Guid.CreateVersion7());

        Assert.False(model.IsLoaded);
        Assert.False(await model.SaveAsync());
        Assert.False(await model.DeleteAsync());
    }

    private static PlacesViewModel Catalog(TransactionFixture fixture) =>
        new(fixture.Database.Resolve<IPlacesQuery>(), fixture.Database.Resolve<IChangeNotifier>());

    private static PlaceViewModel Card(TransactionFixture fixture) =>
        new(
            fixture.Database.Resolve<IPlacesQuery>(),
            fixture.Database.Resolve<IRenamePlaceHandler>(),
            fixture.Database.Resolve<IDeletePlaceHandler>());

    /// <summary>Считает походы в базу: утверждение «список не перечитывается» иначе не проверить.</summary>
    private sealed class CountingPlaces : IPlacesQuery
    {
        private readonly IPlacesQuery _inner;

        public CountingPlaces(IPlacesQuery inner) => _inner = inner;

        public int Reads { get; private set; }

        public Task<IReadOnlyList<PlaceListItem>> ReadAsync(CancellationToken cancellationToken = default)
        {
            Reads++;

            return _inner.ReadAsync(cancellationToken);
        }
    }
}
