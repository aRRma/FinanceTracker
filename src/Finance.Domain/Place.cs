namespace Finance.Domain;

/// <summary>
/// Место операции: магазин, работодатель, банк, клиника. Справочник, а не свободная
/// строка — иначе «Пятерочка», «Пятёрочка» и «5ка» остались бы тремя значениями
/// навсегда, а переименование правит все операции разом.
/// </summary>
public sealed class Place : Entity
{
    private Place(
        Guid key,
        string name,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId)
        : base(key, createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId)
    {
        Name = name;
    }

    /// <summary>Название места. Хранится обрезанным.</summary>
    public string Name { get; private set; }

    /// <summary>Заводит место. Уникальность имени проверяется отдельно: она требует справочника целиком.</summary>
    public static Place Create(string name, DateTimeOffset nowUtc) =>
        new(Keys.New(), Names.Normalize(name, "место"),
            createdAtUtc: nowUtc, updatedAtUtc: nowUtc,
            deletedAtUtc: null, syncedAtUtc: null, externalId: null);

    /// <summary>
    /// ВОССТАНОВЛЕНИЕ ИЗ ХРАНИЛИЩА. Инварианты не проверяются: строка в базе уже
    /// прошла проверку при вводе, а повторная превратила бы чтение в валидацию.
    /// Для создания места этот путь не годится — есть <see cref="Create"/>.
    /// </summary>
    public static Place Restore(
        Guid key,
        string name,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId) =>
        new(key, name, createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId);

    /// <summary>Переименовывает место. Операции при этом не правятся — они ссылаются на ключ.</summary>
    public void Rename(string name) => Name = Names.Normalize(name, "место");
}
