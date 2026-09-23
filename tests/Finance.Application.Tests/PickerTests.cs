using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Tests;

/// <summary>
/// Экраны выбора счёта, подкатегории и места и то, как их решение доезжает
/// до формы операции. Экран выбора уходит обычным «назад» и кладёт решение
/// в общий объект: другого способа рассказать форме о выборе у него нет.
/// </summary>
public sealed class PickerTests
{
    /// <summary>
    /// Выбранный счёт доезжает до формы и меняет валюту суммы вместе с собой.
    /// </summary>
    [Fact]
    public async Task Выбранный_счёт_подставляется_в_форму()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid rubles = await given.AccountAsync("Карта", 1000m);
        Guid euros = await given.AccountAsync("Валютный", 500m, Currency.EUR);

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        form.SourceAccount = form.Accounts.First(account => account.Key == rubles);
        form.PressKey("1");

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(selected: rubles, excluded: null, forTarget: false);

        picker.Pick(Row(picker, euros));
        form.ApplyPicks();

        Assert.Equal(euros, form.SourceAccount?.Key);
        Assert.Equal(Currency.EUR, form.SourceAccount?.Currency);

        // Набранное поход за счётом переживает: форма при возврате не перечитывается
        Assert.Equal("1", form.Amount);
    }

    /// <summary>
    /// Выбор забирается один раз: следующее появление формы ничего не меняет.
    /// </summary>
    [Fact]
    public async Task Выбор_забирается_один_раз()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 1000m);
        Guid cash = await given.AccountAsync("Наличные", 100m);

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(selected: null, excluded: null, forTarget: false);

        picker.Pick(Row(picker, cash));
        form.ApplyPicks();

        Assert.Equal(cash, form.SourceAccount?.Key);

        form.SourceAccount = form.Accounts.First(account => account.Key == card);
        form.ApplyPicks();

        Assert.Equal(card, form.SourceAccount?.Key);
    }

    /// <summary>
    /// У перевода тот же экран выбирает счёт зачисления, и счёт списания из него
    /// исключён: перевод на себя запрещён доменом.
    /// </summary>
    [Fact]
    public async Task Счёт_списания_не_предлагается_для_зачисления()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 1000m);
        Guid cash = await given.AccountAsync("Наличные", 100m);

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        form.Kind = TransactionKind.Transfer;
        form.SourceAccount = form.Accounts.First(account => account.Key == card);

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(selected: null, excluded: card, forTarget: true);

        Guid[] offered = [.. picker.Sections.SelectMany(section => section).Select(row => row.Key)];

        Assert.DoesNotContain(card, offered);

        picker.Pick(Row(picker, cash));
        form.ApplyPicks();

        Assert.Equal(cash, form.TargetAccount?.Key);
    }

    /// <summary>
    /// Закрытый счёт не предлагается: записывать на него нечего.
    /// </summary>
    [Fact]
    public async Task Закрытый_счёт_не_предлагается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 1000m);
        Guid old = await given.AccountAsync("Старая карта");
        await given.CloseAsync(old);

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(selected: null, excluded: null, forTarget: false);

        Assert.DoesNotContain(old, picker.Sections.SelectMany(section => section).Select(row => row.Key));
    }

    /// <summary>
    /// Разные валюты стоят разными разделами: вместе со счётом меняется валюта суммы.
    /// </summary>
    [Fact]
    public async Task Счета_разложены_по_валютам()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 1000m);
        await given.AccountAsync("Валютный", 500m, Currency.EUR);

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(selected: null, excluded: null, forTarget: false);

        Assert.Equal(2, picker.Sections.Count);
        Assert.Equal(["Рубли", "Евро"], picker.Sections.Select(section => section.Title));
    }

    /// <summary>
    /// Группы свёрнуты: подкатегория появляется только после разворота.
    /// </summary>
    [Fact]
    public async Task Группы_категорий_свёрнуты_до_разворота()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.DoesNotContain(picker.Lines, line => line.IsSubcategory);

        CategoryPickerLine group = picker.Lines[0];
        picker.Toggle(group);

        Assert.Contains(picker.Lines, line => line.IsSubcategory);
    }

    /// <summary>
    /// Универсальная расходная группа предлагается и при выборе для дохода:
    /// возврату место в той же статье, где лежит трата. Односторонняя — нет.
    /// </summary>
    [Fact]
    public async Task Универсальная_группа_предлагается_обоим_видам()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.GroupAsync("Маркетплейсы", CategoryKind.Expense, acceptsAnyKind: true);
        await given.GroupAsync("Питомцы", CategoryKind.Expense);

        CategoryPickerViewModel expenses = CategoryPicker(given);
        await expenses.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.Contains("Маркетплейсы", expenses.Lines.Select(line => line.Name));
        Assert.Contains("Питомцы", expenses.Lines.Select(line => line.Name));

        CategoryPickerViewModel incomes = CategoryPicker(given);
        await incomes.LoadAsync(CategoryKind.Income, selected: null);

        Assert.Contains("Маркетплейсы", incomes.Lines.Select(line => line.Name));
        Assert.DoesNotContain("Питомцы", incomes.Lines.Select(line => line.Name));
    }

    /// <summary>
    /// Подкатегория универсальной расходной группы, выбранная для дохода, доезжает
    /// до формы: список формы отбирается тем же правилом, что и экран выбора, —
    /// иначе выбор возврата молча пропадал бы, а поле оставалось пустым.
    /// </summary>
    [Fact]
    public async Task Подкатегория_универсальной_группы_подставляется_в_доход()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 1000m);

        Guid group = await given.GroupAsync("Маркетплейсы", CategoryKind.Expense, acceptsAnyKind: true);
        Guid subcategory = await given.SubcategoryAsync(group, "Возврат заказа");

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        form.Kind = TransactionKind.Income;

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(form.CategoryKind, selected: null);

        picker.Toggle(picker.Lines.First(line => line.Key == group));
        picker.Pick(picker.Lines.First(line => line.Key == subcategory));
        form.ApplyPicks();

        Assert.Equal(subcategory, form.Category?.Key);
    }

    /// <summary>
    /// Поиск раскрывает группы сам и оставляет только подходящие строки.
    /// </summary>
    [Fact]
    public async Task Поиск_категорий_раскрывает_группы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid group = await given.GroupAsync("Питомцы", CategoryKind.Expense);
        await given.SubcategoryAsync(group, "Корм коту");

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        picker.Filter = "Корм кот";

        Assert.Equal(["Питомцы", "Корм коту"], picker.Lines.Select(line => line.Name));
    }

    /// <summary>
    /// Выбранная подкатегория доезжает до формы, а группа не выбирается вовсе.
    /// </summary>
    [Fact]
    public async Task Выбранная_подкатегория_подставляется_в_форму()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 1000m);

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(form.CategoryKind, selected: null);

        CategoryPickerLine group = picker.Lines[0];
        picker.Pick(group);
        form.ApplyPicks();

        Assert.Null(form.Category);

        picker.Toggle(group);

        CategoryPickerLine subcategory = picker.Lines.First(line => line.IsSubcategory);
        picker.Pick(subcategory);
        form.ApplyPicks();

        Assert.Equal(subcategory.Key, form.Category?.Key);
    }

    /// <summary>
    /// Выбранное место доезжает до формы, а снятие очищает поле.
    /// </summary>
    [Fact]
    public async Task Место_подставляется_и_снимается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 1000m);
        await given.SaveAsync(given.Expense(card, 100m, place: "Пятёрочка"));

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        PlacePickerViewModel picker = PlacePicker(given);
        await picker.LoadAsync(current: null);

        picker.Pick(picker.Rows[0]);
        form.ApplyPicks();

        Assert.Equal("Пятёрочка", form.PlaceName);

        picker.ClearPlace();
        form.ApplyPicks();

        Assert.Equal(string.Empty, form.PlaceName);
    }

    /// <summary>
    /// Набранное название, которого в справочнике нет, предлагается завести.
    /// Само место здесь не заводится: оно появится вместе с операцией, и брошенная
    /// форма не оставит пустышку в справочнике.
    /// </summary>
    [Fact]
    public async Task Новое_место_заводится_из_набранного()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 1000m);
        await given.SaveAsync(given.Expense(card, 100m, place: "Пятёрочка"));

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        PlacePickerViewModel picker = PlacePicker(given);
        await picker.LoadAsync(current: null);

        picker.Filter = "Пятёрочка";

        Assert.False(picker.CanCreate);

        picker.Filter = "Магнит";

        Assert.True(picker.CanCreate);
        Assert.Equal("Создать «Магнит»", picker.CreateCaption);
        Assert.Empty(picker.Rows);

        picker.Create();
        form.ApplyPicks();

        Assert.Equal("Магнит", form.PlaceName);
        Assert.Single(await given.Database.Resolve<IPlacesQuery>().ReadAsync());
    }

    private static TransactionViewModel Form(TransactionFixture fixture) => new(
        fixture.Database.Resolve<ITransactionFormQuery>(),
        fixture.Database.Resolve<ITransactionCardQuery>(),
        fixture.Database.Resolve<ISaveTransactionHandler>(),
        fixture.Database.Resolve<IDeleteTransactionHandler>(),
        fixture.Database.Resolve<IAccountsQuery>(),
        fixture.Database.Resolve<IClock>(),
        fixture.Database.Resolve<TransactionPicks>());

    private static AccountPickerViewModel AccountPicker(TransactionFixture fixture) => new(
        fixture.Database.Resolve<IAccountsQuery>(),
        fixture.Database.Resolve<TransactionPicks>());

    private static CategoryPickerViewModel CategoryPicker(TransactionFixture fixture) => new(
        fixture.Database.Resolve<ICategoriesQuery>(),
        fixture.Database.Resolve<IFrequentCategoriesQuery>(),
        fixture.Database.Resolve<TransactionPicks>());

    private static PlacePickerViewModel PlacePicker(TransactionFixture fixture) => new(
        fixture.Database.Resolve<IPlacesQuery>(),
        fixture.Database.Resolve<TransactionPicks>());

    private static AccountPickerRow Row(AccountPickerViewModel picker, Guid account) =>
        picker.Sections.SelectMany(section => section).First(row => row.Key == account);
}
