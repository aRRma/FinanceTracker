using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Rules;

namespace Finance.Domain.Tests;

/// <summary>
/// Правила категорий: уровни, вид, перенос, приёмник и неудаляемые категории.
/// </summary>
public sealed class CategoryInvariantsTests
{
    [Fact]
    [Trait("Инвариант", nameof(Invariant.TwoCategoryLevels))]
    public void Уровень_задаётся_ссылкой_на_группу()
    {
        Category group = Given.Group();
        Category subcategory = Given.Subcategory(group);

        Assert.True(group.IsGroup);
        Assert.True(subcategory.IsSubcategory);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TwoCategoryLevels))]
    public void Третьего_уровня_не_существует()
    {
        Category group = Given.Group();
        Category subcategory = Given.Subcategory(group);

        Assert.Throws<ArgumentException>(
            () => Category.CreateSubcategory(subcategory, "Пятёрочка", "basket", Given.NowUtc));
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.SubcategoryBelongsToGroup))]
    public void Группа_ни_на_что_не_ссылается_а_подкатегория_обязана()
    {
        Category group = Given.Group();
        Category subcategory = Given.Subcategory(group);

        Assert.Null(group.ParentKey);
        Assert.Equal(group.Key, subcategory.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.KindInheritedFromGroup))]
    public void Вид_задан_у_группы_и_не_задан_у_подкатегории()
    {
        Category group = Given.Group(kind: CategoryKind.Income);
        Category subcategory = Given.Subcategory(group);

        Assert.Equal(CategoryKind.Income, group.Kind);
        Assert.Null(subcategory.Kind);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.MoveKeepsKind))]
    public void Перенос_в_группу_того_же_вида_разрешён()
    {
        Category food = Given.Group("Еда");
        Category home = Given.Group("Жильё");
        Category groceries = Given.Subcategory(food);

        groceries.MoveTo(food, home, [], []);

        Assert.Equal(home.Key, groceries.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.MoveKeepsKind))]
    public void Перенос_с_возвратами_в_одностороннюю_группу_отвергается()
    {
        // Вид групп совпадает, но в универсальной статье лежат и возвраты — доходы,
        // которых односторонняя расходная группа не примет
        Category food = Given.Group("Еда", acceptsAnyKind: true);
        Category home = Given.Group("Жильё");
        Category groceries = Given.Subcategory(food);

        DomainException error = Assert.Throws<DomainException>(
            () => groceries.MoveTo(food, home, [], [TransactionKind.Expense, TransactionKind.Income]));

        Assert.Equal(Invariant.MoveKeepsKind, error.Invariant);
        Assert.Equal(food.Key, groceries.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.MoveKeepsKind))]
    public void Перенос_с_возвратами_в_универсальную_группу_разрешён()
    {
        Category food = Given.Group("Еда", acceptsAnyKind: true);
        Category home = Given.Group("Жильё", acceptsAnyKind: true);
        Category groceries = Given.Subcategory(food);

        groceries.MoveTo(food, home, [], [TransactionKind.Expense, TransactionKind.Income]);

        Assert.Equal(home.Key, groceries.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.MoveKeepsKind))]
    public void Перенос_одних_расходов_в_одностороннюю_группу_разрешён()
    {
        Category food = Given.Group("Еда", acceptsAnyKind: true);
        Category home = Given.Group("Жильё");
        Category groceries = Given.Subcategory(food);

        groceries.MoveTo(food, home, [], [TransactionKind.Expense]);

        Assert.Equal(home.Key, groceries.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameUnique))]
    public void Перенос_в_группу_с_таким_же_именем_отвергается()
    {
        // Переезд меняет область уникальности: в новой группе имя может быть занято
        Category food = Given.Group("Еда");
        Category home = Given.Group("Жильё");
        Category other = Given.Subcategory(food, "Прочие траты");

        DomainException error = Assert.Throws<DomainException>(
            () => other.MoveTo(food, home, ["  прочие ТРАТЫ "], []));

        Assert.Equal(Invariant.NameUnique, error.Invariant);
        Assert.Equal(food.Key, other.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.MoveKeepsKind))]
    public void Перенос_в_группу_другого_вида_отвергается()
    {
        Category food = Given.Group("Еда");
        Category salary = Given.Group("Доходы", CategoryKind.Income);
        Category groceries = Given.Subcategory(food);

        DomainException error = Assert.Throws<DomainException>(() => groceries.MoveTo(food, salary, [], []));

        Assert.Equal(Invariant.MoveKeepsKind, error.Invariant);
        Assert.Equal(food.Key, groceries.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ServiceGroupClosedToMoves))]
    public void Перенос_в_служебную_группу_отвергается()
    {
        // Вид у обеих групп расходный, поэтому проверка вида такой перенос пропускает
        Category food = Given.Group("Еда");
        Category service = Given.Group("Служебное", role: CategoryRole.Service);
        Category groceries = Given.Subcategory(food);

        DomainException error = Assert.Throws<DomainException>(() => groceries.MoveTo(food, service, [], []));

        Assert.Equal(Invariant.ServiceGroupClosedToMoves, error.Invariant);
        Assert.Equal(food.Key, groceries.ParentKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryLevelFixed))]
    public void Группу_нельзя_перенести_в_другую_группу()
    {
        Category food = Given.Group("Еда");
        Category home = Given.Group("Жильё");

        DomainException error = Assert.Throws<DomainException>(() => food.MoveTo(food, home, [], []));

        Assert.Equal(Invariant.CategoryLevelFixed, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.GroupHasReceiver))]
    public void Группа_без_приёмника_отвергается()
    {
        Category food = Given.Group();
        Category groceries = Given.Subcategory(food);

        DomainException error = Assert.Throws<DomainException>(
            () => CategoryRules.EnsureHasReceiver(food, [groceries]));

        Assert.Equal(Invariant.GroupHasReceiver, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.GroupHasReceiver))]
    public void Группа_с_единственным_приёмником_принимается()
    {
        Category food = Given.Group();
        Category groceries = Given.Subcategory(food);
        Category other = Given.Subcategory(food, "Прочее", CategoryRole.Other);

        CategoryRules.EnsureHasReceiver(food, [groceries, other]);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.GroupHasReceiver))]
    public void Служебной_группе_приёмник_не_нужен()
    {
        Category service = Given.Group("Служебные", CategoryKind.Expense, CategoryRole.Service);
        Category adjustment = Given.Subcategory(service, "Разница", CategoryRole.Service);

        CategoryRules.EnsureHasReceiver(service, [adjustment]);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.GroupHasReceiver))]
    public void Приёмником_бывает_только_подкатегория()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => Category.CreateGroup("Еда", CategoryKind.Expense, "dots", Given.NowUtc, CategoryRole.Other));

        Assert.Equal(Invariant.GroupHasReceiver, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.DeletedSubcategoryGoesToReceiver))]
    public void Операции_удаляемой_подкатегории_переезжают_в_приёмник()
    {
        Category food = Given.Group();
        Category groceries = Given.Subcategory(food);
        Category other = Given.Subcategory(food, "Прочее", CategoryRole.Other);

        Category receiver = CategoryRules.ReceiverFor(food, [groceries, other], groceries);

        Assert.Equal(other.Key, receiver.Key);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.DeletedSubcategoryGoesToReceiver))]
    public void Приёмник_чужой_группы_не_годится()
    {
        // Иначе операции удаляемой подкатегории уехали бы в другую группу отчёта
        Category food = Given.Group("Еда");
        Category home = Given.Group("Жильё");
        Category groceries = Given.Subcategory(food);
        Category otherOfHome = Given.Subcategory(home, "Прочее", CategoryRole.Other);

        Assert.Throws<ArgumentException>(
            () => CategoryRules.ReceiverFor(food, [groceries, otherOfHome], groceries));
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ProtectedCategoryStays))]
    public void Приёмник_не_удаляется()
    {
        Category food = Given.Group();
        Category other = Given.Subcategory(food, "Прочее", CategoryRole.Other);

        DomainException error = Assert.Throws<DomainException>(() => other.Delete(Given.NowUtc));

        Assert.Equal(Invariant.ProtectedCategoryStays, error.Invariant);
        Assert.False(other.IsDeleted);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ProtectedCategoryStays))]
    public void Служебная_подкатегория_не_удаляется()
    {
        Category service = Given.Group("Служебные", CategoryKind.Expense, CategoryRole.Service);
        Category adjustment = Given.Subcategory(service, "Разница", CategoryRole.Service);

        DomainException error = Assert.Throws<DomainException>(() => adjustment.Delete(Given.NowUtc));

        Assert.Equal(Invariant.ProtectedCategoryStays, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ProtectedCategoryStays))]
    public void Приёмник_не_переносится_в_другую_группу()
    {
        Category food = Given.Group("Еда");
        Category home = Given.Group("Жильё");
        Category other = Given.Subcategory(food, "Прочее", CategoryRole.Other);

        DomainException error = Assert.Throws<DomainException>(() => other.MoveTo(food, home, [], []));

        Assert.Equal(Invariant.ProtectedCategoryStays, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ProtectedCategoryStays))]
    public void Обычная_подкатегория_удаляется()
    {
        Category food = Given.Group();
        Category groceries = Given.Subcategory(food);

        groceries.Delete(Given.NowUtc);

        Assert.True(groceries.IsDeleted);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.GroupNotDeleted))]
    public void Группа_не_удаляется()
    {
        Category food = Given.Group();

        DomainException error = Assert.Throws<DomainException>(() => food.Delete(Given.NowUtc));

        Assert.Equal(Invariant.GroupNotDeleted, error.Invariant);
        Assert.False(food.IsDeleted);
    }

    [Fact]
    public void Пустой_значок_не_нарушение_требования_а_ошибка_вызова()
    {
        // Значок не имя и по каталогу не проверяется: пустым он не приходит,
        // потому что форма подставляет значок группы
        Assert.Throws<ArgumentException>(
            () => Category.CreateGroup("Еда", CategoryKind.Expense, "  ", Given.NowUtc));
    }

    [Fact]
    public void Неизвестный_ключ_значка_принимается()
    {
        Category group = Category.CreateGroup("Еда", CategoryKind.Expense, "нет-такого-значка", Given.NowUtc);

        Assert.Equal("нет-такого-значка", group.Icon);
    }

    [Fact]
    public void Переименование_приёмника_разрешено()
    {
        Category food = Given.Group();
        Category other = Given.Subcategory(food, "Прочее", CategoryRole.Other);

        other.Rename("Разное");

        Assert.Equal("Разное", other.Name);
        Assert.Equal(CategoryRole.Other, other.Role);
    }
}
