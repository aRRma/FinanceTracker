using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Features.Settings.Appearance;

/// <summary>Строка выбора темы: название, пояснение и признак выбранной.</summary>
public sealed record ThemeOption
{
    /// <summary>Какую тему ставит строка.</summary>
    public required Theme Theme { get; init; }

    /// <summary>Название темы.</summary>
    public required string Caption { get; init; }

    /// <summary>Ключ значка строки.</summary>
    public required string Icon { get; init; }

    /// <summary>Пояснение под названием. Пусто у тех строк, которым нечего пояснять.</summary>
    public required string? Hint { get; init; }

    /// <summary>Строка выбрана сейчас — у неё стоит галочка.</summary>
    public required bool IsSelected { get; init; }

    /// <summary>Пояснению есть чем быть — строке нужна вторая строка.</summary>
    public bool HasHint => Hint is not null;
}
