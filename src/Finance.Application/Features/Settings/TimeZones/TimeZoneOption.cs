namespace Finance.Application.Features.Settings.TimeZones;

/// <summary>Строка списка зон: идентификатор, смещение и признак выбранной.</summary>
public sealed record TimeZoneOption
{
    /// <summary>Идентификатор зоны. Пусто у строки «Как в системе».</summary>
    public required string? Id { get; init; }

    /// <summary>Чем строка подписана: идентификатор зоны или «Как в системе».</summary>
    public required string Caption { get; init; }

    /// <summary>
    /// Смещение от UTC на сегодня. Именно на сегодня: у зон с переходом на летнее
    /// время оно разное зимой и летом, и постоянное сбивало бы с толку полгода.
    /// </summary>
    public required string Offset { get; init; }

    /// <summary>Зона выбрана сейчас — у неё стоит галочка.</summary>
    public required bool IsSelected { get; init; }
}
