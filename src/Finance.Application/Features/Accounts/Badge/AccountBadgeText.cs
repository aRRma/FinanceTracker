using Finance.Application.Infrastructure;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Цвет и значок словами: строка карточки «Голубой · процент» и озвучивание сеток выбора.
/// </summary>
public static class AccountBadgeText
{
    /// <summary>
    /// Название цвета с заглавной — им начинается строка карточки и подпись ячейки.
    /// </summary>
    /// <param name="color">Цвет счёта.</param>
    public static string ColorName(AccountColor color) => color switch
    {
        AccountColor.Blue => UiTexts.AccountColorBlue,
        AccountColor.Orange => UiTexts.AccountColorOrange,
        AccountColor.Sky => UiTexts.AccountColorSky,
        AccountColor.Green => UiTexts.AccountColorGreen,
        AccountColor.Violet => UiTexts.AccountColorViolet,
        AccountColor.Magenta => UiTexts.AccountColorMagenta,
        AccountColor.Red => UiTexts.AccountColorRed,
        AccountColor.Pink => UiTexts.AccountColorPink,
        _ => string.Empty
    };

    /// <summary>
    /// Строка карточки: цвет и значок через точку, значок — со строчной, как часть фразы.
    /// </summary>
    /// <param name="color">Цвет счёта.</param>
    /// <param name="icon">Ключ значка, которым счёт рисуется.</param>
    public static string Describe(AccountColor color, string icon) =>
        string.Create(UiCulture.Current, $"{ColorName(color)} · {IconNames.Of(icon).ToLower(UiCulture.Current)}");
}
