using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Accounts.Catalog;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Экран справочника счетов: разделы, строки и перетаскивание.
/// </summary>
public sealed class AccountCatalogTests
{
    private static readonly DateOnly OpenedOn = new(2026, 1, 1);

    /// <summary>
    /// Счёт попадает в раздел по признакам, а не по порядку: заблокированный — в заблокированные,
    /// даже если он скрытый, скрытый действующий — в накопления.
    /// </summary>
    [Fact]
    public async Task Счета_разложены_по_разделам()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid card = await SaveAsync(database, Command("Карта"));
        Guid deposit = await SaveAsync(database, Command("Вклад") with { ExcludedFromTotals = true });
        Guid old = await SaveAsync(database, Command("Старая") with { ExcludedFromTotals = true, IsClosed = true });

        AccountsViewModel model = await LoadedAsync(database);

        Assert.Equal([card], model.Spendable.Select(row => row.Key));
        Assert.Equal([deposit], model.Savings.Select(row => row.Key));
        Assert.Equal([old], model.Closed.Select(row => row.Key));
        Assert.True(model.HasSavings);
        Assert.True(model.HasClosed);
    }

    /// <summary>
    /// Пустой раздел не показывает заголовка: без строк под ним он читался бы
    /// потерянными данными.
    /// </summary>
    [Fact]
    public async Task Пустые_разделы_скрыты()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Command("Карта"));

        AccountsViewModel model = await LoadedAsync(database);

        Assert.Single(model.Spendable);
        Assert.False(model.HasSavings);
        Assert.False(model.HasClosed);
    }

    /// <summary>
    /// Строка несёт значок по типу, подпись «тип · валюта», баланс и его знак.
    /// Накопления получают свой значок, а не значок наличных.
    /// </summary>
    [Fact]
    public async Task Строка_счёта_собрана_из_его_признаков()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid cash = await SaveAsync(database, Command("Наличные") with { OpeningBalance = -250m });
        Guid deposit = await SaveAsync(database, Command("Вклад") with { ExcludedFromTotals = true, OpeningBalance = 1000m });

        AccountsViewModel model = await LoadedAsync(database);

        AccountRowItem cashRow = model.Spendable.Single(row => row.Key == cash);
        AccountRowItem depositRow = model.Savings.Single(row => row.Key == deposit);

        Assert.Equal("Наличные", cashRow.Name);
        Assert.Equal($"{UiTexts.AccountTypeCash} · {Currency.RUB}", cashRow.Caption);
        Assert.Equal(Money.Create(-250m, Currency.RUB).Display, cashRow.Balance);
        Assert.True(cashRow.IsNegative);
        Assert.True(cashRow.IsOpen);

        Assert.False(depositRow.IsNegative);
        Assert.NotEqual(cashRow.Icon, depositRow.Icon);
    }

    /// <summary>
    /// Перетащенный счёт встаёт на место того, на который его бросили, и порядок
    /// сохраняется: после перечитывания он тот же.
    /// </summary>
    [Fact]
    public async Task Перетаскивание_меняет_порядок_и_сохраняет_его()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid first = await SaveAsync(database, Command("Первый"));
        Guid second = await SaveAsync(database, Command("Второй"));
        Guid third = await SaveAsync(database, Command("Третий"));

        AccountsViewModel model = await LoadedAsync(database);

        await model.MoveAsync(Row(model, third), Row(model, first));

        Assert.Equal([third, first, second], model.Spendable.Select(row => row.Key));

        AccountsViewModel reloaded = await LoadedAsync(database);

        Assert.Equal([third, first, second], reloaded.Spendable.Select(row => row.Key));
    }

    /// <summary>
    /// Между разделами счёт не переезжает: раздел — признак счёта, и перетаскивание
    /// молча снимало бы «скрытый».
    /// </summary>
    [Fact]
    public async Task Перетаскивание_между_разделами_не_действует()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid card = await SaveAsync(database, Command("Карта"));
        Guid deposit = await SaveAsync(database, Command("Вклад") with { ExcludedFromTotals = true });

        AccountsViewModel model = await LoadedAsync(database);

        await model.MoveAsync(Row(model, deposit), Row(model, card));

        Assert.Equal([card], model.Spendable.Select(row => row.Key));
        Assert.Equal([deposit], model.Savings.Select(row => row.Key));
    }

    private static AccountRowItem Row(AccountsViewModel model, Guid key) =>
        model.Spendable.Concat(model.Savings).Concat(model.Closed).Single(row => row.Key == key);

    private static async Task<AccountsViewModel> LoadedAsync(TestDatabase database)
    {
        AccountsViewModel model = database.Resolve<AccountsViewModel>();
        await model.LoadAsync();

        return model;
    }

    private static SaveAccountCommand Command(string name) =>
        new()
        {
            Name = name,
            Type = AccountType.Cash,
            Currency = Currency.RUB,
            OpeningBalance = 0m,
            OpenedOn = OpenedOn,
            ExcludedFromTotals = false,
            IsClosed = false
        };

    private static Task<Guid> SaveAsync(TestDatabase database, SaveAccountCommand command) =>
        database.Resolve<ISaveAccountHandler>().HandleAsync(command);
}
