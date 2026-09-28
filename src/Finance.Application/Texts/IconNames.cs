using System.Resources;
using Finance.Application.Infrastructure;

namespace Finance.Application.Texts;

/// <summary>
/// Названия значков набора для чтения вслух.
/// </summary>
/// <remarks>
/// Ресурс свой, а не строки <see cref="UiTexts"/>: ключ здесь — ключ значка из набора,
/// а не имя свойства, и полноту против набора сверяет тест, как ключи правил против
/// их перечисления. Фасад написан руками по той же причине: восемь десятков свойств
/// с искажёнными дефисами именами читать по ключу было бы нечем.
/// </remarks>
public static class IconNames
{
    private static readonly ResourceManager Manager =
        new("Finance.Application.Texts.IconNames", typeof(IconNames).Assembly);

    /// <summary>
    /// Название значка.
    /// </summary>
    /// <param name="key">Ключ значка из набора.</param>
    /// <returns>
    /// Название; для ключа без названия — сам ключ: латиница вслух хуже слова,
    /// но лучше молчания.
    /// </returns>
    public static string Of(string key) => Manager.GetString(key, UiCulture.Current) ?? key;
}
