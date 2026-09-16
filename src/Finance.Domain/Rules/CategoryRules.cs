using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Errors;

namespace Finance.Domain.Rules;

/// <summary>
/// Проверки категорий, которым мало одной категории: наличие приёмника, выбор
/// того, куда переедут операции удаляемой подкатегории, и допустимость переноса.
/// </summary>
/// <remarks>
/// Соседние категории подаются уже прочитанными: домену запрещено ходить
/// в хранилище. Несовпадение ссылок здесь — не ввод пользователя,
/// а ошибка вызывающего кода, поэтому <see cref="ArgumentException"/>.
/// </remarks>
public static class CategoryRules
{
    /// <summary>
    /// У каждой группы есть приёмник «Прочее». Исключение — служебные
    /// группы: их единственная подкатегория неудаляема, и девать операции некуда
    /// просто потому, что удалять нечего.
    /// </summary>
    /// <param name="group">Проверяемая группа.</param>
    /// <param name="subcategories">Неудалённые подкатегории этой группы.</param>
    public static void EnsureHasReceiver(Category group, IReadOnlyCollection<Category> subcategories) =>
        FindReceiver(group, subcategories);

    /// <summary>
    /// Подкатегория, в которую переедут операции удаляемой. Вызывающая
    /// сторона переносит операции и удаляет подкатегорию одной транзакцией — иначе
    /// операции останутся висеть на удалённой категории.
    /// </summary>
    /// <param name="group">Группа удаляемой подкатегории.</param>
    /// <param name="subcategories">Неудалённые подкатегории этой группы.</param>
    /// <param name="deleted">Удаляемая подкатегория.</param>
    public static Category ReceiverFor(
        Category group,
        IReadOnlyCollection<Category> subcategories,
        Category deleted)
    {
        ArgumentNullException.ThrowIfNull(deleted);

        DomainException.ThrowIf(
            deleted.IsProtected,
            Invariant.ProtectedCategoryStays,
            RuleText.ProtectedCategoryNotDeleted,
            deleted.Name);

        EnsureBelongsToGroup(deleted, group, nameof(deleted));

        return FindReceiver(group, subcategories)
               ?? throw new DomainException(
                   Invariant.GroupHasReceiver,
                   RuleTexts.Format(RuleText.ServiceGroupHasNoReceiver, group.Name));
    }

    /// <summary>
    /// Проверяет, что подкатегорию можно перенести в другую группу: та обязана
    /// быть группой того же вида и не служебной, сама подкатегория — не приёмником
    /// и не служебной, а имя — свободным в новой группе.
    /// </summary>
    /// <param name="subcategory">Переносимая подкатегория.</param>
    /// <param name="currentGroup">Группа, в которой она находится сейчас.</param>
    /// <param name="newGroup">Группа, в которую она переезжает.</param>
    /// <param name="namesInNewGroup">Имена неудалённых подкатегорий новой группы.</param>
    public static void EnsureCanMove(
        Category subcategory,
        Category currentGroup,
        Category newGroup,
        IEnumerable<string> namesInNewGroup)
    {
        ArgumentNullException.ThrowIfNull(subcategory);
        ArgumentNullException.ThrowIfNull(namesInNewGroup);

        DomainException.ThrowIf(
            subcategory.IsGroup,
            Invariant.CategoryLevelFixed,
            RuleText.GroupNotMoved,
            subcategory.Name);

        DomainException.ThrowIf(
            subcategory.IsProtected,
            Invariant.ProtectedCategoryStays,
            RuleText.ProtectedCategoryNotMoved,
            subcategory.Name);

        EnsureIsGroup(currentGroup, nameof(currentGroup));
        EnsureIsGroup(newGroup, nameof(newGroup));
        EnsureBelongsToGroup(subcategory, currentGroup, nameof(currentGroup));

        // Служебная группа замкнута на своей единственной подкатегории: приёмника
        // ей не положено, а из отчёта она исключена, поэтому переехавшая
        // категория пропала бы из него молча
        DomainException.ThrowIf(
            newGroup.Role is CategoryRole.Service,
            Invariant.ServiceGroupClosedToMoves,
            RuleText.ServiceGroupClosedToMoves,
            subcategory.Name, newGroup.Name);

        // Вид подкатегория не хранит — она его наследует, поэтому сравниваются группы.
        // Подстановок пять, перегрузки ThrowIf на столько нет: текст собирается
        // внутри ветки, то есть по-прежнему только при нарушении
        if (currentGroup.Kind != newGroup.Kind)
        {
            throw new DomainException(
                Invariant.MoveKeepsKind,
                RuleTexts.Format(
                    RuleText.MoveKeepsKind,
                    subcategory.Name, currentGroup.Name, currentGroup.Kind, newGroup.Name, newGroup.Kind));
        }

        NameUniqueness.Ensure(
            subcategory.Name,
            namesInNewGroup,
            RuleText.SubjectSubcategoryOfGroup,
            newGroup.Name);
    }

    /// <summary>
    /// Переданная категория — группа, а не подкатегория.
    /// </summary>
    /// <param name="category">Проверяемая категория.</param>
    /// <param name="parameterName">Имя параметра вызывающего метода для сообщения об ошибке.</param>
    internal static void EnsureIsGroup(Category category, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(category, parameterName);

        if (!category.IsGroup)
        {
            throw new ArgumentException(DomainFaults.NotAGroup(category.Name), parameterName);
        }
    }

    /// <summary>
    /// Ищет приёмник группы одним проходом. Заодно проверяет, что все переданные
    /// подкатегории принадлежат именно ей: с чужой «Прочее» в списке операции
    /// удаляемой подкатегории уехали бы в другую группу — молча и необратимо.
    /// </summary>
    /// <returns>Приёмник или <c>null</c>, если группа служебная и приёмник ей не положен.</returns>
    private static Category? FindReceiver(Category group, IReadOnlyCollection<Category> subcategories)
    {
        EnsureIsGroup(group, nameof(group));
        ArgumentNullException.ThrowIfNull(subcategories);

        Category? receiver = null;
        int receivers = 0;

        foreach (Category subcategory in subcategories)
        {
            EnsureBelongsToGroup(subcategory, group, nameof(subcategories));

            if (subcategory.Role is CategoryRole.Other)
            {
                receiver = subcategory;
                receivers++;
            }
        }

        if (group.Role is CategoryRole.Service)
        {
            DomainException.ThrowIf(
                subcategories.Count != 1 || receivers != 0,
                Invariant.GroupHasReceiver,
                RuleText.ServiceGroupHasOneSubcategory,
                group.Name);

            return null;
        }

        DomainException.ThrowIf(
            receivers != 1,
            Invariant.GroupHasReceiver,
            RuleText.GroupHasOneReceiver,
            group.Name, receivers);

        return receiver;
    }

    private static void EnsureBelongsToGroup(Category subcategory, Category group, string parameterName)
    {
        if (subcategory.ParentKey != group.Key)
        {
            throw new ArgumentException(
                DomainFaults.NotInGroup(subcategory.Name, group.Name),
                parameterName);
        }
    }
}
