using Finance.Application.Infrastructure;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Ячейка сетки цветов: сам цвет, его название для озвучивания и отметка выбранного.
/// </summary>
/// <param name="Color">Цвет.</param>
/// <param name="Name">Название цвета — TalkBack читает его вместо пятна.</param>
/// <param name="IsSelected">Этот цвет у счёта сейчас.</param>
public sealed record AccountColorChoice(AccountColor Color, string Name, bool IsSelected)
{
    /// <summary>
    /// Знак на ячейке: галочка у выбранного, у остальных пусто — одна заливка.
    /// </summary>
    public string? Glyph => IsSelected ? "check" : null;

    /// <summary>
    /// Что прочтёт озвучка: название цвета, а у выбранного — ещё и что он выбран.
    /// </summary>
    public string Description => IsSelected ? string.Format(UiCulture.Current, UiTexts.IconChosen, Name) : Name;
}
