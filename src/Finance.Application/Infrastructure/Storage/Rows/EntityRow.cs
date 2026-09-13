namespace Finance.Application.Infrastructure.Storage.Rows;

/// <summary>
/// Общая часть строки любой таблицы: ключ, технические метки и надгробие.
/// Как сущность не отображается — EF наследует её поля в каждую таблицу,
/// а сама она в модели не участвует.
/// </summary>
internal abstract class EntityRow
{
    /// <summary>Единственный идентификатор записи. Генерируется клиентом, базой — никогда.</summary>
    public required Guid Key { get; init; }

    /// <summary>Момент создания записи. Участвует в сортировке ленты.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Момент последнего изменения. Проставляется единой точкой сохранения.</summary>
    public required DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Надгробие мягкого удаления.</summary>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>Подтверждение доставки на сервер. В MVP всегда пусто: обмена нет.</summary>
    public DateTimeOffset? SyncedAtUtc { get; set; }

    /// <summary>Резерв под будущий импорт из сторонних приложений.</summary>
    public string? ExternalId { get; set; }
}
