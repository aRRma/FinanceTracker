using Finance.Application.Features.Categories.Card;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using static Finance.Application.Tests.CategorySetup;

namespace Finance.Application.Tests;

/// <summary>
/// Справочник категорий: заведение, переименование, перенос и порядок показа.
/// </summary>
public sealed class CategoriesTests
{
    /// <summary>
    /// Заведение группы создаёт приёмник «Прочее» в той же транзакции: группа
    /// без него не примет операции удаляемых подкатегорий.
    /// </summary>
    [Fact]
    public async Task Созданная_группа_получает_Прочее()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Дом", CategoryKind.Expense));

        IReadOnlyList<CategoryListItem> categories = await ReadAsync(database);

        CategoryListItem receiver = Assert.Single(categories, item => item.ParentKey == group);
        Assert.Equal("Прочее", receiver.Name);
        Assert.Equal(CategoryRole.Other, receiver.Role);
        Assert.Equal(CategoryKind.Expense, receiver.Kind);
        Assert.True(receiver.IsProtected);
    }

    /// <summary>
    /// Подкатегория наследует вид группы: своего у неё нет и в списке она с видом группы.
    /// </summary>
    [Fact]
    public async Task Подкатегория_показывается_с_видом_своей_группы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Подработка", CategoryKind.Income));
        Guid subcategory = await SaveAsync(database, Subcategory(group, "Консультации"));

        CategoryListItem item = Assert.Single(await ReadAsync(database), found => found.Key == subcategory);

        Assert.Equal(CategoryKind.Income, item.Kind);
        Assert.False(item.IsGroup);
    }

    /// <summary>
    /// Имя группы занято: сравнение не различает регистр и не считает окружающие пробелы.
    /// </summary>
    [Fact]
    public async Task Занятое_имя_группы_блокирует_сохранение()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Group("Еда", CategoryKind.Expense));

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, Group("  еда  ", CategoryKind.Expense)));

        Assert.Equal(Invariant.NameUnique, failure.Invariant);
    }

    /// <summary>
    /// Имя уникально среди групп своего вида, а не по всему справочнику: расходная
    /// «Еда» и доходная «Еда» — разные понятия, и запрет здесь был бы выдуманным.
    /// </summary>
    [Fact]
    public async Task Одноимённые_группы_разных_видов_допустимы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Group("Подарки", CategoryKind.Expense));
        await SaveAsync(database, Group("Подарки", CategoryKind.Income));

        IReadOnlyList<CategoryListItem> groups = [.. (await ReadAsync(database)).Where(item => item.IsGroup)];

        Assert.Equal(2, groups.Count);
    }

    /// <summary>
    /// Имя подкатегории занято внутри своей группы.
    /// </summary>
    [Fact]
    public async Task Занятое_имя_подкатегории_блокирует_сохранение()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Транспорт", CategoryKind.Expense));
        await SaveAsync(database, Subcategory(group, "Такси"));

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, Subcategory(group, "такси")));

        Assert.Equal(Invariant.NameUnique, failure.Invariant);
    }

    /// <summary>
    /// Область уникальности подкатегории — её группа, а не весь справочник.
    /// </summary>
    [Fact]
    public async Task Одноимённые_подкатегории_в_разных_группах_допустимы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid transport = await SaveAsync(database, Group("Транспорт", CategoryKind.Expense));
        Guid entertainment = await SaveAsync(database, Group("Развлечения", CategoryKind.Expense));

        await SaveAsync(database, Subcategory(transport, "Аренда"));
        await SaveAsync(database, Subcategory(entertainment, "Аренда"));

        IReadOnlyList<CategoryListItem> categories = await ReadAsync(database);

        Assert.Equal(2, categories.Count(item => item.Name == "Аренда"));
    }

    /// <summary>
    /// Перенос меняет группу, но не трогает ключ: операции ссылаются именно на него.
    /// </summary>
    [Fact]
    public async Task Подкатегория_переносится_в_группу_того_же_вида()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid transport = await SaveAsync(database, Group("Транспорт", CategoryKind.Expense));
        Guid car = await SaveAsync(database, Group("Автомобиль", CategoryKind.Expense));
        Guid taxi = await SaveAsync(database, Subcategory(transport, "Такси"));

        await SaveAsync(database, new SaveCategoryCommand
        {
            Key = taxi,
            ParentKey = car,
            Name = "Такси",
            Icon = "car"
        });

        CategoryListItem moved = Assert.Single(await ReadAsync(database), item => item.Key == taxi);

        Assert.Equal(car, moved.ParentKey);
    }

    /// <summary>
    /// Вид наследуется от группы, поэтому перенос в чужой вид перевернул бы знак операций.
    /// </summary>
    [Fact]
    public async Task Перенос_в_группу_другого_вида_отвергается()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid expenses = await SaveAsync(database, Group("Транспорт", CategoryKind.Expense));
        Guid incomes = await SaveAsync(database, Group("Подработка", CategoryKind.Income));
        Guid taxi = await SaveAsync(database, Subcategory(expenses, "Такси"));

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, new SaveCategoryCommand
            {
                Key = taxi,
                ParentKey = incomes,
                Name = "Такси",
                Icon = "car"
            }));

        Assert.Equal(Invariant.MoveKeepsKind, failure.Invariant);
    }

    /// <summary>
    /// Служебная группа замкнута: переехавшая в неё категория пропала бы из отчёта молча.
    /// </summary>
    [Fact]
    public async Task Перенос_в_служебную_группу_отвергается()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        IReadOnlyList<CategoryListItem> preset = await ReadAsync(database);

        CategoryListItem service = ServiceGroupOf(preset);
        CategoryListItem taxi = preset.Single(item => item.Name == "Такси");

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, new SaveCategoryCommand
            {
                Key = taxi.Key,
                ParentKey = service.Key,
                Name = taxi.Name,
                Icon = taxi.Icon
            }));

        Assert.Equal(Invariant.ServiceGroupClosedToMoves, failure.Invariant);
    }

    /// <summary>
    /// Вторая подкатегория в служебной группе недопустима: приёмника ей не положено,
    /// и найти его потом было бы уже невозможно.
    /// </summary>
    [Fact]
    public async Task Подкатегория_в_служебную_группу_не_заводится()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        CategoryListItem service = ServiceGroupOf(await ReadAsync(database));

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => SaveAsync(database, Subcategory(service.Key, "Ещё одна")));

        Assert.Equal(Invariant.GroupHasReceiver, failure.Invariant);
    }

    /// <summary>
    /// Правка одного регистра сохраняется: уникальность его не различает, а пользователь видит.
    /// </summary>
    [Fact]
    public async Task Переименование_в_другом_регистре_сохраняется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("транспорт", CategoryKind.Expense));

        await SaveAsync(database, new SaveCategoryCommand
        {
            Key = group,
            Name = "Транспорт",
            Icon = "train"
        });

        CategoryListItem renamed = Assert.Single(await ReadAsync(database), item => item.Key == group);

        Assert.Equal("Транспорт", renamed.Name);
    }

    /// <summary>
    /// Порядок задаётся запросом, а не экраном: сначала группа, за ней её
    /// подкатегории по алфавиту, «Прочее» — последним.
    /// </summary>
    [Fact]
    public async Task Прочее_показывается_последним_в_своей_группе()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        await SaveAsync(database, Subcategory(group, "Продукты"));
        await SaveAsync(database, Subcategory(group, "Кафе"));

        string[] order = [.. (await ReadAsync(database)).Select(item => item.Name)];

        Assert.Equal(["Еда", "Кафе", "Продукты", "Прочее"], order);
    }

    /// <summary>
    /// Служебные группы уходят в конец списка: их правят реже всего.
    /// </summary>
    [Fact]
    public async Task Служебные_группы_показываются_последними()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        IReadOnlyList<CategoryListItem> groups = [.. (await ReadAsync(database)).Where(item => item.IsGroup)];

        // От первой служебной группы до конца списка — только служебные, и они есть:
        // служебная посреди списка оставила бы после себя обычные
        IReadOnlyList<CategoryListItem> tail = [.. groups.SkipWhile(group => group.Role is not CategoryRole.Service)];

        Assert.NotEmpty(tail);
        Assert.All(tail, group => Assert.Equal(CategoryRole.Service, group.Role));
    }

    /// <summary>
    /// Операции удаляемой подкатегории переезжают в приёмник её группы. Суммы и даты
    /// не трогаются: переезжает только ссылка на категорию.
    /// </summary>
    [Fact]
    public async Task Удаление_подкатегории_переносит_операции_в_приёмник()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 100m));
        await fixture.SaveAsync(fixture.Expense(account, 250m));

        CategoryDeletion deletion = await DeletionAsync(fixture, fixture.ExpenseCategory);

        await fixture.Database.Resolve<IDeleteSubcategoryHandler>().HandleAsync(fixture.ExpenseCategory);

        await using FinanceDbContext context = await fixture.Database.Contexts.CreateDbContextAsync();

        Guid receiver = await context.Categories
            .Where(row => row.Name == deletion.ReceiverName && row.Role == CategoryRole.Other)
            .Join(
                context.Categories.Where(parent => parent.Name == deletion.GroupName),
                row => row.ParentKey,
                parent => parent.Key,
                (row, _) => row.Key)
            .SingleAsync();

        Assert.Empty(await context.Transactions.Where(row => row.CategoryKey == fixture.ExpenseCategory).ToListAsync());
        Assert.Equal(2, await context.Transactions.CountAsync(row => row.CategoryKey == receiver));
        Assert.Equal([100m, 250m], await context.Transactions.OrderBy(row => row.Amount).Select(row => row.Amount).ToListAsync());
        Assert.Empty(await context.Categories.Where(row => row.Key == fixture.ExpenseCategory).ToListAsync());
    }

    /// <summary>
    /// Переехавшие операции помечаются изменёнными. Правка мимо <c>SaveChanges</c>
    /// оставила бы метку старой, и переезд не уехал бы в будущий обмен — на втором
    /// устройстве операции навсегда остались бы на удалённой категории.
    /// </summary>
    [Fact]
    public async Task Перенос_операций_метит_их_как_изменённые()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 100m));

        DateTimeOffset before = await fixture.StampAsync(transaction);

        await fixture.Database.Resolve<IDeleteSubcategoryHandler>().HandleAsync(fixture.ExpenseCategory);

        Assert.True(await fixture.StampAsync(transaction) > before, "метка изменения операции не обновилась");
    }

    /// <summary>
    /// Мягко удалённая операция остаётся на удалённой подкатегории: ссылка на неё
    /// допустима, а воскрешать удалённое ради переезда незачем.
    /// </summary>
    [Fact]
    public async Task Удалённые_операции_на_удалённой_подкатегории_остаются()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 100m));

        await fixture.Database.Resolve<IDeleteTransactionHandler>().HandleAsync(transaction);
        await fixture.Database.Resolve<IDeleteSubcategoryHandler>().HandleAsync(fixture.ExpenseCategory);

        await using FinanceDbContext context = await fixture.Database.Contexts.CreateDbContextAsync();

        TransactionRow row = await context.Transactions
            .IgnoreQueryFilters()
            .SingleAsync(deleted => deleted.Key == transaction);

        Assert.Equal(fixture.ExpenseCategory, row.CategoryKey);
    }

    /// <summary>
    /// Без приёмника группе некуда девать операции удаляемых подкатегорий.
    /// </summary>
    [Fact]
    public async Task Прочее_не_удаляется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Еда", CategoryKind.Expense));

        CategoryListItem receiver = Assert.Single(await ReadAsync(database), item => item.ParentKey == group);

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => database.Resolve<IDeleteSubcategoryHandler>().HandleAsync(receiver.Key));

        Assert.Equal(Invariant.ProtectedCategoryStays, failure.Invariant);
    }

    /// <summary>
    /// Опустевшая группа остаётся в справочнике: удаления групп в приложении нет.
    /// </summary>
    [Fact]
    public async Task Группа_не_удаляется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Еда", CategoryKind.Expense));

        DomainException failure = await Assert.ThrowsAsync<DomainException>(
            () => database.Resolve<IDeleteSubcategoryHandler>().HandleAsync(group));

        Assert.Equal(Invariant.GroupNotDeleted, failure.Invariant);
    }

    /// <summary>
    /// Диалог называет группу и приёмник: по ним видно, куда именно уедут операции.
    /// </summary>
    [Fact]
    public async Task Диалог_называет_группу_и_приёмник()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Транспорт", CategoryKind.Expense));
        Guid taxi = await SaveAsync(database, Subcategory(group, "Такси"));

        CategoryDeletion deletion = await database.Resolve<ICategoryDeletionQuery>().ReadAsync(taxi)
                                    ?? throw new InvalidOperationException("Подкатегория не читается");

        Assert.Equal("Такси", deletion.Name);
        Assert.Equal("Транспорт", deletion.GroupName);
        Assert.Equal("Прочее", deletion.ReceiverName);
        Assert.Equal(0, deletion.TransactionCount);
    }

    /// <summary>
    /// Счётчик считает то же, что переедет: мягко удалённые операции не переезжают.
    /// </summary>
    [Fact]
    public async Task Счётчик_операций_для_диалога_считает_только_неудалённые()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        await fixture.SaveAsync(fixture.Expense(account, 100m));
        Guid removed = await fixture.SaveAsync(fixture.Expense(account, 250m));

        await fixture.Database.Resolve<IDeleteTransactionHandler>().HandleAsync(removed);

        CategoryDeletion deletion = await DeletionAsync(fixture, fixture.ExpenseCategory);

        Assert.Equal(1, deletion.TransactionCount);
    }

    /// <summary>
    /// У приёмника и группы удалять нечего — диалогу об этом и сказать нечего.
    /// </summary>
    [Fact]
    public async Task Диалог_не_читается_для_группы_и_приёмника()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid group = await SaveAsync(database, Group("Еда", CategoryKind.Expense));
        CategoryListItem receiver = Assert.Single(await ReadAsync(database), item => item.ParentKey == group);

        ICategoryDeletionQuery query = database.Resolve<ICategoryDeletionQuery>();

        Assert.Null(await query.ReadAsync(group));
        Assert.Null(await query.ReadAsync(receiver.Key));
        Assert.Null(await query.ReadAsync(Guid.CreateVersion7()));
    }

    /// <summary>
    /// Переименование не трогает операции: они ссылаются на ключ, а не на имя.
    /// </summary>
    [Fact]
    public async Task Переименование_подкатегории_не_трогает_операции()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта", 1000m);
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 100m));

        CategoryDeletion before = await DeletionAsync(fixture, fixture.ExpenseCategory);

        await fixture.Database.Resolve<ISaveCategoryHandler>().HandleAsync(new SaveCategoryCommand
        {
            Key = fixture.ExpenseCategory,
            ParentKey = null,
            Name = "Совсем другое имя",
            Icon = "basket"
        });

        await using FinanceDbContext context = await fixture.Database.Contexts.CreateDbContextAsync();

        TransactionRow row = await context.Transactions.SingleAsync(item => item.Key == transaction);

        Assert.Equal(fixture.ExpenseCategory, row.CategoryKey);
        Assert.NotEqual("Совсем другое имя", before.Name);
    }

    private static async Task<CategoryDeletion> DeletionAsync(TransactionFixture fixture, Guid key) =>
        await fixture.Database.Resolve<ICategoryDeletionQuery>().ReadAsync(key)
        ?? throw new InvalidOperationException($"Подкатегория {key} не читается");

    private static CategoryListItem ServiceGroupOf(IReadOnlyList<CategoryListItem> categories) =>
        categories.Single(item => item.IsGroup && item.Role is CategoryRole.Service);

    private static Task<IReadOnlyList<CategoryListItem>> ReadAsync(TestDatabase database) =>
        database.Resolve<ICategoriesQuery>().ReadAsync();
}
