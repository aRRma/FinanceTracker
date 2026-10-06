using System.Resources;
using Finance.Application.Infrastructure;

namespace Finance.Application.Texts;

/// <summary>
/// Названия городов, которыми подписан список часовых поясов.
/// </summary>
/// <remarks>
/// Ресурс свой, а не строки <see cref="UiTexts"/>: ключ здесь — идентификатор зоны
/// с косой чертой, а не имя свойства, и полноту против списка зон сверяет тест,
/// как названия значков против набора.
/// </remarks>
public static class CityNames
{
    private static readonly ResourceManager Manager =
        new("Finance.Application.Texts.CityNames", typeof(CityNames).Assembly);

    /// <summary>
    /// Название города зоны.
    /// </summary>
    /// <param name="zoneId">Идентификатор зоны.</param>
    /// <returns>Название; для зоны без названия — сам идентификатор: латиница хуже слова, но лучше пустой строки.</returns>
    public static string Of(string zoneId) => Manager.GetString(zoneId, UiCulture.Current) ?? zoneId;
}
