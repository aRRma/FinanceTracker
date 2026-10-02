using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

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
        await picker.LoadAsync(TransactionKind.Expense, selected: rubles, excluded: null, forTarget: false);

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
        await picker.LoadAsync(TransactionKind.Expense, selected: null, excluded: null, forTarget: false);

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
        await picker.LoadAsync(TransactionKind.Transfer, selected: null, excluded: card, forTarget: true);

        Guid[] offered = [.. picker.Sections.SelectMany(section => section).Select(row => row.Key)];

        Assert.DoesNotContain(card, offered);

        picker.Pick(Row(picker, cash));
        form.ApplyPicks();

        Assert.Equal(cash, form.TargetAccount?.Key);
    }

    /// <summary>
    /// Заблокированный счёт не предлагается: записывать на него нечего.
    /// </summary>
    [Fact]
    public async Task Заблокированный_счёт_не_предлагается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 1000m);
        Guid old = await given.AccountAsync("Старая карта");
        await given.CloseAsync(old);

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(TransactionKind.Expense, selected: null, excluded: null, forTarget: false);

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
        await picker.LoadAsync(TransactionKind.Expense, selected: null, excluded: null, forTarget: false);

        Assert.Equal(2, picker.Sections.Count);
        Assert.Equal(["Рубли", "Евро"], picker.Sections.Select(section => section.Title));
    }

    /// <summary>
    /// Заголовок называет сторону только у перевода: у дохода «счёт списания»
    /// читался бы ошибкой — деньги на него приходят.
    /// </summary>
    [Theory]
    [InlineData(TransactionKind.Expense, false)]
    [InlineData(TransactionKind.Income, false)]
    [InlineData(TransactionKind.Transfer, false)]
    [InlineData(TransactionKind.Transfer, true)]
    public async Task Заголовок_выбора_зависит_от_вида(TransactionKind kind, bool forTarget)
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await given.AccountAsync("Карта", 1000m);

        AccountPickerViewModel picker = AccountPicker(given);
        await picker.LoadAsync(kind, selected: null, excluded: null, forTarget: forTarget);

        string expected = kind is not TransactionKind.Transfer ? UiTexts.PickAccountTitle
            : forTarget ? UiTexts.PickAccountTarget
            : UiTexts.PickAccountSource;

        Assert.Equal(expected, picker.Title);
    }

    /// <summary>
    /// Счёт, заведённый с экрана выбора, встаёт в форму вместо стоявшего: форма,
    /// открывая выбор, уже ждёт новый счёт. Возврат с выбора без нового счёта
    /// стоявший не трогает.
    /// </summary>
    [Fact]
    public async Task Счёт_заведённый_с_выбора_встаёт_в_форму()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        Guid card = await given.AccountAsync("Карта", 1000m);

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);
        form.PressKey("7");

        form.AwaitNewAccount(target: false);
        await form.TakeNewAccountAsync();

        Assert.Equal(card, form.SourceAccount?.Key);

        form.AwaitNewAccount(target: false);
        Guid cash = await given.AccountAsync("Наличные");
        form.ApplyPicks();
        await form.TakeNewAccountAsync();

        Assert.Equal(cash, form.SourceAccount?.Key);
        Assert.Equal("7", form.Amount);
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
    /// «Прочее» в раскрытой группе стоит первым, хотя по алфавиту оно последнее,
    /// а поиск находит его как раньше.
    /// </summary>
    [Fact]
    public async Task Прочее_первым_в_раскрытой_группе()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid group = await given.GroupAsync("Питомцы", CategoryKind.Expense);
        await given.SubcategoryAsync(group, "Ветеринар");
        await given.SubcategoryAsync(group, "Корм");
        CategoryListItem other = await OtherAsync(given, group);

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        CategoryPickerLine header = picker.Lines.First(line => line.Key == group);
        picker.Toggle(header);

        int index = picker.Lines.IndexOf(header);

        Assert.Equal(
            [other.Name, "Ветеринар", "Корм"],
            picker.Lines.Skip(index + 1).Take(3).Select(line => line.Name));

        picker.Filter = other.Name;

        Assert.Contains(picker.Lines, line => line.Key == other.Key);
    }

    /// <summary>
    /// Группа из одной подкатегории не раскрывается, а выбирается сама и отдаёт
    /// форме эту подкатегорию; выбранной она помечена сама, без раскрытия.
    /// </summary>
    [Fact]
    public async Task Группа_из_одной_подкатегории_выбирается_одним_касанием()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 1000m);

        Guid group = await given.GroupAsync("Питомцы", CategoryKind.Expense);
        CategoryListItem only = await OtherAsync(given, group);

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(form.CategoryKind, selected: null);

        CategoryPickerLine header = picker.Lines.First(line => line.Key == group);
        int count = picker.Lines.Count;

        Assert.False(header.IsExpandable);

        picker.Toggle(header);

        Assert.Equal(count, picker.Lines.Count);

        picker.Pick(header);
        form.ApplyPicks();

        Assert.Equal(only.Key, form.Category?.Key);

        // «Прочее» в форме подписано с группой: одно слово не сказало бы, чьё оно
        Assert.Equal($"Питомцы · {only.Name}", form.CategoryCaption);

        CategoryPickerViewModel again = CategoryPicker(given);
        await again.LoadAsync(form.CategoryKind, selected: only.Key);

        Assert.True(again.Lines.First(line => line.Key == group).IsSelected);
        Assert.DoesNotContain(again.Lines, line => line.Key == only.Key);

        // Найденная по названию подкатегории группа показывает и её: одна шапка
        // не объяснила бы, почему группа нашлась. По названию группы — только шапку
        again.Filter = only.Name;

        // Выбор отмечен один раз — строкой подкатегории, а не ещё и шапкой над ней
        Assert.True(again.Lines.Single(line => line.Key == only.Key).IsSelected);
        Assert.False(again.Lines.Single(line => line.Key == group).IsSelected);

        again.Filter = "Питомцы";

        Assert.Equal(["Питомцы"], again.Lines.Select(line => line.Name));
        Assert.True(again.Lines.Single().IsSelected);
    }

    /// <summary>
    /// «Прочее» подписывается с группой, приёмник с названием своей группы — без
    /// повтора, обычная подкатегория — одним названием.
    /// </summary>
    [Fact]
    public void Подпись_прочего_называет_группу()
    {
        Assert.Equal("Транспорт · Прочее", CategoryCaption.Of("Прочее", "Транспорт", CategoryRole.Other));
        Assert.Equal("Без категории", CategoryCaption.Of("Без категории", "Без категории", CategoryRole.Other));
        Assert.Equal("Такси", CategoryCaption.Of("Такси", "Транспорт", CategoryRole.Normal));
    }

    /// <summary>
    /// Для дохода «Без категории» — среди доходных групп, а расходной «Без категории»
    /// в разделе возвратов нет: она не универсальная, и непонятный приход не вычитается
    /// из неразобранных трат.
    /// </summary>
    [Fact]
    public async Task Для_дохода_предлагается_только_доходная_без_категории()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid space = Preset.Embedded().Namespace;

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(CategoryKind.Income, selected: null);

        int refunds = picker.Lines.IndexOf(picker.Lines.Single(static line => line.IsSection));
        int unsorted = picker.Lines.IndexOf(picker.Lines.Single(line => line.Key == Keys.Derive(space, "unsorted_inc")));

        Assert.True(unsorted < refunds);
        Assert.DoesNotContain(picker.Lines, line => line.Key == Keys.Derive(space, "unsorted_exp"));
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
    /// У дохода сначала доходные группы, затем подпись раздела и расходные группы,
    /// принимающие доход как возврат: иначе доход тонул в расходных статьях.
    /// У расхода другого вида нет — нет и подписи.
    /// </summary>
    [Fact]
    public async Task Группы_другого_вида_идут_своим_разделом()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.GroupAsync("Маркетплейсы", CategoryKind.Expense, acceptsAnyKind: true);
        await given.GroupAsync("Подработка", CategoryKind.Income);

        CategoryPickerViewModel incomes = CategoryPicker(given);
        await incomes.LoadAsync(CategoryKind.Income, selected: null);

        int section = IndexOfSection(incomes);
        string[] names = [.. incomes.Lines.Select(line => line.Name)];

        Assert.Equal(UiTexts.PickCategoryRefunds, names[section]);
        Assert.Contains("Подработка", names[..section]);
        Assert.Contains("Маркетплейсы", names[(section + 1)..]);
        Assert.Single(incomes.Lines, line => line.IsSection);

        CategoryPickerViewModel expenses = CategoryPicker(given);
        await expenses.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.DoesNotContain(expenses.Lines, line => line.IsSection);
    }

    /// <summary>
    /// Поиск показывает подпись раздела, только если нашлось что-то в нём, и
    /// разворот группы над подписью её не задевает.
    /// </summary>
    [Fact]
    public async Task Подпись_раздела_живёт_с_поиском_и_разворотом()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid marketplaces = await given.GroupAsync("Маркетплейсы", CategoryKind.Expense, acceptsAnyKind: true);
        await given.SubcategoryAsync(marketplaces, "Возврат заказа");
        Guid side = await given.GroupAsync("Подработка", CategoryKind.Income);
        await given.SubcategoryAsync(side, "Переводы текстов");

        CategoryPickerViewModel picker = CategoryPicker(given);
        await picker.LoadAsync(CategoryKind.Income, selected: null);

        picker.Filter = "Возврат заказа";

        Assert.Equal(
            [UiTexts.PickCategoryRefunds, "Маркетплейсы", "Возврат заказа"],
            picker.Lines.Select(line => line.Name));

        picker.Filter = "Переводы текстов";

        Assert.DoesNotContain(picker.Lines, line => line.IsSection);

        picker.Filter = string.Empty;

        CategoryPickerLine group = picker.Lines.First(line => line.Key == side);
        int before = IndexOfSection(picker);

        picker.Toggle(group);
        Assert.Equal(before + group.Count, IndexOfSection(picker));

        picker.Toggle(group);
        Assert.Equal(before, IndexOfSection(picker));
        Assert.True(picker.Lines[before].IsSection);
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

    private static TransactionViewModel Form(TransactionFixture fixture) =>
        fixture.Database.Resolve<TransactionViewModel>();

    // «Прочее», которое группа получает при заведении
    private static async Task<CategoryListItem> OtherAsync(TransactionFixture fixture, Guid group)
    {
        IReadOnlyList<CategoryListItem> all = await fixture.Database.Resolve<ICategoriesQuery>().ReadAsync();

        return all.Single(item => item.ParentKey == group && item.Role is CategoryRole.Other);
    }

    private static int IndexOfSection(CategoryPickerViewModel picker)
    {
        for (int index = 0; index < picker.Lines.Count; index++)
        {
            if (picker.Lines[index].IsSection)
            {
                return index;
            }
        }

        return -1;
    }

    private static AccountPickerViewModel AccountPicker(TransactionFixture fixture) =>
        fixture.Database.Resolve<AccountPickerViewModel>();

    private static CategoryPickerViewModel CategoryPicker(TransactionFixture fixture) =>
        fixture.Database.Resolve<CategoryPickerViewModel>();

    private static PlacePickerViewModel PlacePicker(TransactionFixture fixture) =>
        fixture.Database.Resolve<PlacePickerViewModel>();

    private static AccountPickerRow Row(AccountPickerViewModel picker, Guid account) =>
        picker.Sections.SelectMany(section => section).First(row => row.Key == account);
}
