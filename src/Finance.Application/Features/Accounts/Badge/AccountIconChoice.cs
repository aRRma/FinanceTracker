using Finance.Application.Infrastructure;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Ячейка сетки значков: ключ, название для озвучивания и отметка выбранного.
/// </summary>
/// <param name="Key">Ключ значка.</param>
/// <param name="Name">Название значка — TalkBack читает его вместо рисунка.</param>
/// <param name="IsSelected">Этим значком счёт рисуется сейчас.</param>
/// <param name="Color">Цвет счёта: выбранная ячейка залита им — видно, каким выйдет знак.</param>
public sealed record AccountIconChoice(string Key, string Name, bool IsSelected, AccountColor Color)
{
    /// <summary>
    /// Что прочтёт озвучка: название, а у выбранного — ещё и что он выбран.
    /// </summary>
    public string Description => IsSelected ? string.Format(UiCulture.Current, UiTexts.IconChosen, Name) : Name;
}
