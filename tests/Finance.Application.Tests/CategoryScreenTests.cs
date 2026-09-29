using Finance.Application.Features.Categories.Card;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using static Finance.Application.Tests.CategorySetup;

namespace Finance.Application.Tests;

/// <summary>
/// Экраны справочника категорий: список, карточка группы и карточка подкатегории.
/// </summary>
public sealed class CategoryScreenTests
{
    /// <summary>
    /// Список показывает подкатегории под своей группой, а не вперемешку.
    /// </summary>
    [Fact]
    public async Task Справочник_показывает_подкатегории_под_своей_группой()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid food = await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        await SaveAsync(database, Subcategory(food, "Продукты"));

        CategoriesViewModel model = Catalog(database);
        await model.LoadAsync();

        CategoryLine group = Assert.Single(model.Lines, line => line.IsGroup);

        Assert.Equal("Еда", group.Name);
        Assert.Equal(2, group.Count);

        // Свёрнутая группа показывает только шапку: подкатегории появляются по развороту
        Assert.DoesNotContain(model.Lines, line => line.IsSubcategory);

        model.Toggle(group);

        Assert.Equal(["Продукты", "Прочее"], model.Lines.Where(line => line.IsSubcategory).Select(line => line.Name));
    }

    /// <summary>
    /// Разворот и сворачивание правят список поштучно и оставляют на месте саму
    /// шапку: полная пересборка приходит в список как сброс, и тот перерисовывает
    /// себя целиком, без плавного появления строк.
    /// </summary>
    [Fact]
    public async Task Разворот_группы_не_пересобирает_список()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid food = await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        await SaveAsync(database, Subcategory(food, "Продукты"));

        Guid home = await SaveAsync(database, Group("Жильё", CategoryKind.Expense));
        await SaveAsync(database, Subcategory(home, "Аренда"));

        CategoriesViewModel model = Catalog(database);
        await model.LoadAsync();

        CategoryLine first = model.Lines[0];
        CategoryLine second = model.Lines[1];

        model.Toggle(first);

        Assert.Same(first, model.Lines[0]);
        Assert.True(first.IsExpanded);
        Assert.Equal("chevron-down", first.ChevronIcon);

        // Строки встали под своей шапкой, соседняя группа осталась той же строкой
        Assert.Equal(2, model.Lines.Count(line => line.IsSubcategory));
        Assert.Same(second, model.Lines[^1]);

        model.Toggle(first);

        Assert.Same(first, model.Lines[0]);
        Assert.Same(second, model.Lines[1]);
        Assert.False(first.IsExpanded);
        Assert.Equal("chevron-right", first.ChevronIcon);
        Assert.DoesNotContain(model.Lines, line => line.IsSubcategory);
    }

    /// <summary>
    /// Переключатель видов перебирает уже прочитанное: его нажимают подряд,
    /// и каждое нажатие стоило бы запроса к базе.
    /// </summary>
    [Fact]
    public async Task Смена_вида_не_читает_базу_заново()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        await SaveAsync(database, Group("Зарплата", CategoryKind.Income));

        CountingCategories categories = new(database.Resolve<ICategoriesQuery>());
        CategoriesViewModel model = new(categories, database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        Assert.Equal("Еда", Assert.Single(model.Lines, line => line.IsGroup).Name);
        Assert.Equal(1, categories.Reads);

        model.Kind = CategoryKind.Income;

        Assert.Equal("Зарплата", Assert.Single(model.Lines, line => line.IsGroup).Name);
        Assert.Equal(1, categories.Reads);
    }

    /// <summary>
    /// Экран перечитывается сам, когда категории правят с другого экрана.
    /// </summary>
    [Fact]
    public async Task Справочник_перечитывается_по_изменению_категорий()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        CategoriesViewModel model = Catalog(database);
        model.Activate();

        try
        {
            await model.LoadAsync();

            Assert.Empty(model.Lines);
            Assert.True(model.IsEmpty);

            await SaveAsync(database, Group("Еда", CategoryKind.Expense));

            Assert.Equal("Еда", Assert.Single(model.Lines, line => line.IsGroup).Name);
        }
        finally
        {
            model.Deactivate();
        }
    }

    /// <summary>
    /// У новой группы вид выбирается, у существующей заперт: его наследуют подкатегории.
    /// </summary>
    [Fact]
    public async Task Вид_группы_запирается_после_создания()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        GroupViewModel model = GroupCard(database);

        Assert.True(model.KindEditable);
        Assert.True(model.ShowPreview);

        model.Name = "Еда";
        model.Kind = CategoryKind.Income;

        Assert.True(await model.SaveAsync(), model.Error);

        Assert.True(model.KindLocked);
        Assert.False(model.ShowPreview);
        Assert.Equal("Доход", model.KindCaption);
    }

    /// <summary>
    /// Пояснение универсальности показано, пока она выбирается, и у заведённой
    /// универсальной группы. У односторонней оно противоречило бы подписи над ним:
    /// там сказано «только операции своего вида».
    /// </summary>
    [Fact]
    public async Task Пояснение_универсальности_не_спорит_с_подписью()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        GroupViewModel universal = GroupCard(database);

        Assert.True(universal.AcceptsAnyKindHintVisible);

        universal.Name = "Маркетплейсы";
        universal.AcceptsAnyKind = true;

        Assert.True(await universal.SaveAsync(), universal.Error);
        Assert.True(universal.AcceptsAnyKindHintVisible);

        GroupViewModel plain = GroupCard(database);
        plain.Name = "Питомцы";

        Assert.True(await plain.SaveAsync(), plain.Error);
        Assert.False(plain.AcceptsAnyKindHintVisible);
    }

    /// <summary>
    /// Пояснение универсальности говорит словами вида группы: у расходной
    /// вычитаются возвраты, у доходной — расходы.
    /// </summary>
    [Fact]
    public async Task Пояснение_универсальности_следует_виду_группы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        GroupViewModel model = GroupCard(database);

        Assert.Equal(UiTexts.GroupAcceptsAnyKindHint, model.AcceptsAnyKindHint);

        model.Kind = CategoryKind.Income;

        Assert.Equal(UiTexts.GroupAcceptsAnyKindHintIncome, model.AcceptsAnyKindHint);
    }

    /// <summary>
    /// Занятое имя показывается текстом рядом с формой, а не роняет экран.
    /// </summary>
    [Fact]
    public async Task Занятое_имя_группы_показывается_ошибкой_на_экране()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Group("Еда", CategoryKind.Expense));

        GroupViewModel model = GroupCard(database);
        model.Name = "еда";

        Assert.False(await model.SaveAsync());
        Assert.True(model.HasError);
        Assert.Contains("занято", model.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Карточка группы показывает её подкатегории, включая «Прочее» без перехода.
    /// </summary>
    [Fact]
    public async Task Карточка_группы_показывает_подкатегории()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid food = await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        await SaveAsync(database, Subcategory(food, "Продукты"));

        GroupViewModel model = GroupCard(database);
        await model.LoadAsync(food);

        Assert.Equal(["Продукты", "Прочее"], model.Subcategories.Select(row => row.Name));
        Assert.True(model.Subcategories[0].IsEditable);
        Assert.False(model.Subcategories[1].IsEditable);
        Assert.True(model.CanAddSubcategory);
    }

    /// <summary>
    /// Значок новой подкатегории подставляется от группы: свой — уточнение, а не обязанность.
    /// </summary>
    [Fact]
    public async Task Значок_подкатегории_наследуется_от_группы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid food = await SaveAsync(database, new SaveCategoryCommand
        {
            Name = "Еда",
            Icon = "basket",
            Kind = CategoryKind.Expense
        });

        SubcategoryViewModel model = SubcategoryCard(database);
        await model.LoadAsync(key: null, group: food);

        Assert.Equal("basket", model.Icon.Selected);
        Assert.Equal("Расход", model.KindCaption);
        Assert.False(model.CanDelete);
    }

    /// <summary>
    /// Сетку значков озвучка читает названиями, выбранный — с пометкой: цвет,
    /// которым он выделен, незрячему ничего не говорит. Заодно видно, что ресурс
    /// названий вшит под тем именем, под которым его ищут.
    /// </summary>
    [Fact]
    public void Значки_озвучиваются_названиями()
    {
        IconPicker picker = new(IconCatalog.Embedded());

        picker.Pick("basket");

        Assert.Equal("Значок: Корзина", picker.SelectedDescription);
        Assert.Equal("Корзина, выбран", picker.Choices.Single(static choice => choice.Key == "basket").Description);
        Assert.Equal("Автобус", picker.Choices.Single(static choice => choice.Key == "bus").Description);
    }

    /// <summary>
    /// Переносить предлагается только в группы того же вида и не в служебные.
    /// </summary>
    [Fact]
    public async Task В_списке_групп_только_группы_того_же_вида()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        IReadOnlyList<CategoryListItem> categories = await database.Resolve<ICategoriesQuery>().ReadAsync();
        CategoryListItem taxi = categories.Single(item => item.Name == "Такси");

        SubcategoryViewModel model = SubcategoryCard(database);
        await model.LoadAsync(taxi.Key, group: null);

        // Сверка с полным ожидаемым составом, а не «все предложенные подходят»:
        // та проверка проходила и на пустом списке, где переносить некуда
        Guid[] expected = [.. categories
            .Where(item => item.IsGroup && item.Kind is CategoryKind.Expense && item.Role is not CategoryRole.Service)
            .Select(item => item.Key)
            .Order()];

        Assert.Contains(taxi.ParentKey!.Value, expected);
        Assert.Equal(expected, model.Groups.Select(option => option.Key).Order());

        Assert.True(model.CanDelete);
    }

    /// <summary>
    /// Диалог называет число операций и приёмник: подтверждать вслепую нечего.
    /// </summary>
    [Fact]
    public async Task Диалог_удаления_называет_число_операций_и_приёмник()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 100m));
        await fixture.SaveAsync(fixture.Expense(account, 250m));

        SubcategoryViewModel model = SubcategoryCard(fixture.Database);
        await model.LoadAsync(fixture.ExpenseCategory, group: null);

        string? prompt = await model.DeletePromptAsync();

        Assert.NotNull(prompt);
        Assert.Contains("2 операции перейдут", prompt, StringComparison.Ordinal);
        Assert.Contains("Прочее", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Пустая подкатегория удаляется без обещаний о переезде: переезжать нечему.
    /// </summary>
    [Fact]
    public async Task Диалог_удаления_молчит_о_переезде_когда_операций_нет()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid food = await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        Guid products = await SaveAsync(database, Subcategory(food, "Продукты"));

        SubcategoryViewModel model = SubcategoryCard(database);
        await model.LoadAsync(products, group: null);

        Assert.Null(await model.DeletePromptAsync());
        Assert.True(await model.DeleteAsync(), model.Error);

        Assert.DoesNotContain(await database.Resolve<ICategoriesQuery>().ReadAsync(), item => item.Key == products);
    }

    /// <summary>
    /// Смена группы в списке — это перенос: сохранение переставляет подкатегорию.
    /// </summary>
    [Fact]
    public async Task Смена_группы_на_карточке_переносит_подкатегорию()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid transport = await SaveAsync(database, Group("Транспорт", CategoryKind.Expense));
        Guid car = await SaveAsync(database, Group("Автомобиль", CategoryKind.Expense));
        Guid taxi = await SaveAsync(database, Subcategory(transport, "Такси"));

        SubcategoryViewModel model = SubcategoryCard(database);
        await model.LoadAsync(taxi, group: null);

        model.Group = model.Groups.Single(option => option.Key == car);

        Assert.True(await model.SaveAsync(), model.Error);

        CategoryListItem moved = (await database.Resolve<ICategoriesQuery>().ReadAsync())
            .Single(item => item.Key == taxi);

        Assert.Equal(car, moved.ParentKey);
    }

    private static CategoriesViewModel Catalog(TestDatabase database) =>
        database.Resolve<CategoriesViewModel>();

    private static GroupViewModel GroupCard(TestDatabase database) =>
        database.Resolve<GroupViewModel>();

    private static SubcategoryViewModel SubcategoryCard(TestDatabase database) =>
        database.Resolve<SubcategoryViewModel>();

    /// <summary>
    /// Считает походы в базу: утверждение «список не перечитывается» иначе не проверить.
    /// </summary>
    private sealed class CountingCategories(ICategoriesQuery inner) : ICategoriesQuery
    {

        public int Reads { get; private set; }

        public Task<IReadOnlyList<CategoryListItem>> ReadAsync(CancellationToken cancellationToken = default)
        {
            Reads++;

            return inner.ReadAsync(cancellationToken);
        }
    }
}
