using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure.Storage.Rows;

/// <summary>
/// Строка таблицы счетов. Одно поле — одна колонка, никаких проверок:
/// инварианты живут в <see cref="Account"/>, а строка только хранит значения.
/// </summary>
internal sealed class AccountRow : EntityRow
{
    /// <summary>
    /// Наименование счёта.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Наличные или карта.
    /// </summary>
    public required AccountType Type { get; set; }

    /// <summary>
    /// Валюта счёта. Из неё же берётся валюта начального остатка и сумм операций.
    /// </summary>
    public required Currency Currency { get; set; }

    /// <summary>
    /// Начальный остаток без валюты: она одна на весь счёт и своей колонки не имеет.
    /// </summary>
    public required decimal OpeningBalance { get; set; }

    /// <summary>
    /// Дата, с которой действует начальный остаток.
    /// </summary>
    public required DateOnly OpenedOn { get; set; }

    /// <summary>
    /// «Скрыть из расчётов».
    /// </summary>
    public required bool ExcludedFromTotals { get; set; }

    /// <summary>
    /// «Счёт закрыт».
    /// </summary>
    public required bool IsClosed { get; set; }

    /// <summary>
    /// Порядок на главном экране.
    /// </summary>
    public required int SortOrder { get; set; }
}
