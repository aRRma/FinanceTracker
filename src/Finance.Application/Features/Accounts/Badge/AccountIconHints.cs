using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Подсказка значка по названию счёта — до трёх значков над сеткой. Только подсказка:
/// сам значок по названию не меняется, иначе переименование меняло бы значок молча.
/// </summary>
public static class AccountIconHints
{
    private const int Limit = 3;

    // Основы слов, а не слова: «вклад», «вклады», «вкладной» ловятся одной основой.
    // Основы — в ресурсах через запятую: они зависят от языка, а не от кода
    private static readonly (Func<string> Stems, string[] Icons)[] Rules =
    [
        (static () => UiTexts.AccountIconHintCredit, ["percentage"]),
        (static () => UiTexts.AccountIconHintSavings, ["building-bank", "coins"]),
        (static () => UiTexts.AccountIconHintTravel, ["plane", "beach"]),
        (static () => UiTexts.AccountIconHintForeign, ["plane", "coins"]),
        (static () => UiTexts.AccountIconHintHome, ["home", "key"]),
        (static () => UiTexts.AccountIconHintCar, ["car"]),
        (static () => UiTexts.AccountIconHintFamily, ["users"]),
        (static () => UiTexts.AccountIconHintGift, ["gift"]),
        (static () => UiTexts.AccountIconHintStudy, ["school"]),
        (static () => UiTexts.AccountIconHintReserve, ["shield"]),
        (static () => UiTexts.AccountIconHintPhone, ["device-mobile"]),
        (static () => UiTexts.AccountIconHintCash, ["cash"])
    ];

    /// <summary>
    /// Значки, подходящие к названию, в порядке правил, без повторов и не больше трёх.
    /// </summary>
    /// <param name="name">Название счёта, как набрано.</param>
    public static IReadOnlyList<string> For(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string text = name.ToLower(UiCulture.Current);
        List<string> found = [];

        foreach ((Func<string> stems, string[] icons) in Rules)
        {
            if (!Matches(text, stems()))
            {
                continue;
            }

            foreach (string icon in icons)
            {
                if (found.Count < Limit && !found.Contains(icon))
                {
                    found.Add(icon);
                }
            }
        }

        return found;
    }

    private static bool Matches(string text, string stems)
    {
        foreach (string stem in stems.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (text.Contains(stem, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
