using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Счёт в списке — на главном экране и в справочнике. Плоская модель чтения:
/// доменный счёт до экрана не доезжает, а баланс в нём и не хранится.
/// </summary>
public sealed record AccountListItem
{
    /// <summary>
    /// Ключ счёта.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Наименование счёта.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Наличные или карта. Влияет только на значок и подпись.
    /// </summary>
    public required AccountType Type { get; init; }

    /// <summary>
    /// Цвет счёта.
    /// </summary>
    public required AccountColor Color { get; init; }

    /// <summary>
    /// Ключ значка, которым счёт рисуется: выбранный руками или по типу.
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Баланс на текущий момент: начальный остаток плюс все операции.
    /// </summary>
    public required Money Balance { get; init; }

    /// <summary>
    /// Начальный остаток — строка ленты счёта, пока операций нет.
    /// </summary>
    public required Money OpeningBalance { get; init; }

    /// <summary>
    /// Дата открытия — подпись начального остатка и нижняя граница даты операции.
    /// </summary>
    public required DateOnly OpenedOn { get; init; }

    /// <summary>
    /// «Скрытый»: счёт не входит в «доступно к тратам» и в итоги дня.
    /// </summary>
    public required bool ExcludedFromTotals { get; init; }

    /// <summary>
    /// «Счёт заблокирован»: выведен из употребления, но лента и отчёт не меняются.
    /// </summary>
    public required bool IsClosed { get; init; }

    /// <summary>
    /// Порядок на главном экране, заданный перетаскиванием.
    /// </summary>
    public required int SortOrder { get; init; }
}
