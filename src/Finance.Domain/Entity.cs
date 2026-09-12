namespace Finance.Domain;

/// <summary>
/// Общее у всех сущностей: ключ, технические метки и мягкое удаление.
/// Вынесено в один тип не ради экономии строк, а ради мягкого удаления: физическое
/// удаление, записанное хотя бы в одном месте, будущий обмен уже не заметит,
/// и запись воскреснет с другого устройства.
/// </summary>
public abstract class Entity
{
    /// <summary>Восстанавливает общую часть сущности из хранилища. Проверок не выполняет.</summary>
    protected Entity(
        Guid key,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId)
    {
        Key = key;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
        SyncedAtUtc = syncedAtUtc;
        ExternalId = externalId;
    }

    /// <summary>Единственный идентификатор сущности. Второго нет и не будет.</summary>
    public Guid Key { get; }

    /// <summary>Момент создания записи. Участвует в сортировке ленты.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Момент последнего изменения. Проставляется при сохранении, а не обработчиком.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Надгробие мягкого удаления. Заполнено — запись исчезла с экранов, но осталась в базе.</summary>
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>Подтверждение доставки на сервер. В MVP всегда пусто: обмена нет.</summary>
    public DateTimeOffset? SyncedAtUtc { get; private set; }

    /// <summary>Резерв под будущий импорт из сторонних приложений.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Запись мягко удалена.</summary>
    public bool IsDeleted => DeletedAtUtc is not null;

    /// <summary>
    /// Удаляет запись мягко — единственный способ удалить что-либо в этой системе.
    /// Повторное удаление метку не сдвигает: иначе при обмене удаление выглядело бы
    /// новее чужой правки только оттого, что кнопку нажали дважды.
    /// </summary>
    public virtual void Delete(DateTimeOffset atUtc) => DeletedAtUtc ??= atUtc;

    /// <summary>Проставляет метку изменения. Вызывается единой точкой сохранения.</summary>
    public void MarkUpdated(DateTimeOffset atUtc) => UpdatedAtUtc = atUtc;
}
