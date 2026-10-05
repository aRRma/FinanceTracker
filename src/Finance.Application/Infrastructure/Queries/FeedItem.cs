using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Строка ленты, как её отдаёт база: операция вместе с названиями всего, на что
/// она ссылается. Плоская модель чтения — доменная операция до экрана не доезжает,
/// а названий счёта, категории и места в ней и нет.
/// </summary>
public sealed record FeedItem
{
    /// <summary>
    /// Ключ операции — по нему открывается карточка.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Вид операции.
    /// </summary>
    public required TransactionKind Kind { get; init; }

    /// <summary>
    /// Дата операции — по ней лента режется на дни.
    /// </summary>
    public required DateOnly OccurredOn { get; init; }

    /// <summary>
    /// Сумма со стороны, с которой смотрят: расход и списание перевода — минусом,
    /// доход и зачисление перевода — плюсом, в валюте счёта этой стороны.
    /// </summary>
    public required Money Amount { get; init; }

    /// <summary>
    /// Ключ счёта, со стороны которого показана строка.
    /// </summary>
    public required Guid AccountKey { get; init; }

    /// <summary>
    /// Название счёта, со стороны которого показана строка.
    /// </summary>
    public required string AccountName { get; init; }

    /// <summary>
    /// Знак счёта, со стороны которого показана строка.
    /// </summary>
    public required AccountMark Account { get; init; }

    /// <summary>
    /// Знак второго счёта перевода — того, что в <see cref="Title"/>. У дохода и расхода пусто.
    /// </summary>
    public AccountMark? OtherAccount { get; init; }

    /// <summary>
    /// Заголовок строки: подкатегория у дохода и расхода, второй счёт у перевода —
    /// тот, что не <see cref="AccountName"/>.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Группа подкатегории. У перевода пусто.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Место. Пусто, если не указано или удалено из справочника.
    /// </summary>
    public string? Place { get; init; }

    /// <summary>
    /// Заметка.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Ключ значка подкатегории. У перевода пусто — у него свой значок.
    /// </summary>
    public string? Icon { get; init; }
}
