namespace Finance.Domain;

/// <summary>
/// Категория: группа первого уровня или подкатегория второго. Ровно два уровня —
/// третий заменён справочником мест, потому что категория отвечает «на что»,
/// а место «где», и смешивать их в одном дереве значило выбирать оба всегда.
/// </summary>
public sealed class Category : Entity
{
    private Category(
        Guid key,
        Guid? parentKey,
        CategoryKind? kind,
        string name,
        string icon,
        CategoryRole role,
        bool excludeFromReports,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId)
        : base(key, createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId)
    {
        ParentKey = parentKey;
        Kind = kind;
        Name = name;
        Icon = icon;
        Role = role;
        ExcludeFromReports = excludeFromReports;
    }

    /// <summary>
    /// Ключ группы. Заполнен у подкатегории и пуст у группы — этим уровень
    /// и задаётся. Отдельного поля уровня нет: два источника одного факта разошлись бы,
    /// а путь восстановления их не сверяет.
    /// </summary>
    public Guid? ParentKey { get; private set; }

    /// <summary>Вид. Задан только у группы; подкатегория наследует его и своего не имеет.</summary>
    public CategoryKind? Kind { get; }

    /// <summary>Название категории.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Ключ значка. По каталогу не проверяется намеренно: неизвестный ключ показывается
    /// запасным значком и ошибкой быть не должен.
    /// </summary>
    public string Icon { get; private set; }

    /// <summary>Роль: обычная, приёмник «Прочее» или служебная.</summary>
    public CategoryRole Role { get; }

    /// <summary>Не показывать в отчёте и не включать в его суммы.</summary>
    public bool ExcludeFromReports { get; }

    /// <summary>Категория — группа первого уровня.</summary>
    public bool IsGroup => ParentKey is null;

    /// <summary>Категория — подкатегория второго уровня.</summary>
    public bool IsSubcategory => ParentKey is not null;

    /// <summary>Категория неудаляема и не переносится: приёмник «Прочее» или служебная.</summary>
    public bool IsProtected => Role is CategoryRole.Other or CategoryRole.Service;

    /// <summary>
    /// Заводит группу. Подкатегория «Прочее» к ней создаётся отдельно и в той же
    /// транзакции: без приёмника группа теряет, куда девать операции удаляемых
    /// подкатегорий.
    /// </summary>
    public static Category CreateGroup(
        string name,
        CategoryKind kind,
        string icon,
        DateTimeOffset nowUtc,
        CategoryRole role = CategoryRole.Normal,
        bool excludeFromReports = false)
    {
        DomainException.ThrowIf(
            role is CategoryRole.Other,
            Invariant.GroupHasReceiver,
            "Приёмником «Прочее» бывает только подкатегория, не группа");

        return new Category(
            Keys.New(), parentKey: null, kind,
            Names.Normalize(name, "группа"), NormalizeIcon(icon),
            role, excludeFromReports,
            createdAtUtc: nowUtc, updatedAtUtc: nowUtc,
            deletedAtUtc: null, syncedAtUtc: null, externalId: null);
    }

    /// <summary>
    /// Заводит подкатегорию внутри группы. Вид не передаётся: он наследуется от
    /// группы, и параметр позволил бы записать подкатегорию с чужим видом.
    /// Первым аргументом идёт группа, в которую подкатегория заводится.
    /// </summary>
    public static Category CreateSubcategory(
        Category parent,
        string name,
        string icon,
        DateTimeOffset nowUtc,
        CategoryRole role = CategoryRole.Normal,
        bool excludeFromReports = false)
    {
        CategoryRules.EnsureIsGroup(parent, nameof(parent));

        return new Category(
            Keys.New(), parent.Key, kind: null,
            Names.Normalize(name, "подкатегория"), NormalizeIcon(icon),
            role, excludeFromReports,
            createdAtUtc: nowUtc, updatedAtUtc: nowUtc,
            deletedAtUtc: null, syncedAtUtc: null, externalId: null);
    }

    /// <summary>
    /// ВОССТАНОВЛЕНИЕ ИЗ ХРАНИЛИЩА. Инварианты не проверяются: строка в базе уже
    /// прошла проверку при вводе, а повторная превратила бы чтение в валидацию.
    /// Для создания категории этот путь не годится — есть <see cref="CreateGroup"/>
    /// и <see cref="CreateSubcategory"/>.
    /// </summary>
    public static Category Restore(
        Guid key,
        Guid? parentKey,
        CategoryKind? kind,
        string name,
        string icon,
        CategoryRole role,
        bool excludeFromReports,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId) =>
        new(key, parentKey, kind, name, icon, role, excludeFromReports,
            createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId);

    /// <summary>
    /// Переименовывает категорию любого уровня, включая «Прочее».
    /// Уникальность имени здесь не проверяется: она зависит от соседей, которых
    /// сущность не видит, — это делает <see cref="NameUniqueness"/>.
    /// </summary>
    public void Rename(string name) => Name = Names.Normalize(name, "категория");

    /// <summary>Меняет значок.</summary>
    public void ChangeIcon(string icon) => Icon = NormalizeIcon(icon);

    /// <summary>
    /// Переносит подкатегорию в другую группу. Допустимость переноса
    /// проверяет <see cref="CategoryRules.EnsureCanMove"/>: она зависит от обеих
    /// групп и их содержимого, а не от одной этой подкатегории.
    /// </summary>
    /// <param name="currentGroup">Группа, в которой подкатегория находится сейчас.</param>
    /// <param name="newGroup">Группа, в которую она переезжает.</param>
    /// <param name="namesInNewGroup">Имена неудалённых подкатегорий новой группы.</param>
    public void MoveTo(Category currentGroup, Category newGroup, IEnumerable<string> namesInNewGroup)
    {
        CategoryRules.EnsureCanMove(this, currentGroup, newGroup, namesInNewGroup);

        ParentKey = newGroup.Key;
    }

    /// <summary>
    /// Удаляет категорию мягко. «Прочее» и служебные не удаляются: без приёмника
    /// группе некуда девать операции удаляемых подкатегорий. Группы не удаляются
    /// вовсе: опустевшая группа остаётся в списке.
    /// </summary>
    public override void Delete(DateTimeOffset atUtc)
    {
        DomainException.ThrowIf(
            IsProtected,
            Invariant.ProtectedCategoryStays,
            $"Категория «{Name}» не удаляется: она приёмник группы или служебная");

        DomainException.ThrowIf(
            IsGroup,
            Invariant.GroupNotDeleted,
            $"Группа «{Name}» не удаляется: опустевшая группа остаётся в списке");

        base.Delete(atUtc);
    }

    /// <summary>
    /// Приводит ключ значка. Не имя и не доменное правило: по каталогу значок не
    /// проверяется, а пустым он не приходит — форма подставляет значок группы,
    /// поэтому пустота здесь означает ошибку вызывающего кода.
    /// </summary>
    private static string NormalizeIcon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);

        return icon.Trim();
    }
}
