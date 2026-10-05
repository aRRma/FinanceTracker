using Finance.Domain.Enums;

namespace Finance.Application.Features.Settings.DefaultAccounts;

/// <summary>
/// Строка выбора счёта по умолчанию: знак, имя, тип с валютой и признак выбранного.
/// </summary>
public sealed record DefaultAccountOption
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
    /// Тип и валюта: «Карта · RUB». Два одноимённых по смыслу счёта в разных валютах так различимы.
    /// </summary>
    public required string Caption { get; init; }

    /// <summary>
    /// Цвет знака счёта.
    /// </summary>
    public required AccountColor Color { get; init; }

    /// <summary>
    /// Ключ значка знака счёта.
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Счёт — счёт по умолчанию, у него стоит галочка.
    /// </summary>
    public required bool IsSelected { get; init; }
}
