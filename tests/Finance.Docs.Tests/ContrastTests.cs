using System.Globalization;
using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Контраст текста к фону не ниже WCAG AA. Требование записано словами, а глазами
/// проверяется плохо: бледная подпись выглядит «просто вторичной», пока её не
/// посчитаешь. Пары перечислены руками — какой текст на каком фоне встречается,
/// из палитры не выводится.
/// </summary>
public sealed partial class ContrastTests
{
    /// <summary>Порог обычного текста по WCAG AA. Крупного текста в приложении нет.</summary>
    private const double Minimum = 4.5;

    private static readonly Lazy<IReadOnlyDictionary<string, Colour>> Palette = new(Read);

    /// <summary>
    /// Текст и фон, встречающиеся вместе. Имена без суффикса темы: каждая пара
    /// сверяется дважды, в светлой и тёмной.
    /// </summary>
    public static TheoryData<string, string, string> Pairs()
    {
        (string Foreground, string Background)[] pairs =
        [
            // Три уровня текста на трёх фонах: страница, карточка, утопленный блок
            ("Ink", "Paper"), ("Ink", "Card"), ("Ink", "Sunk"),
            ("Ink2", "Paper"), ("Ink2", "Card"), ("Ink2", "Sunk"),
            ("Ink3", "Paper"), ("Ink3", "Card"), ("Ink3", "Sunk"),

            // Цвет действия: ссылка в строке, знак выбранного значка
            ("Accent", "Paper"), ("Accent", "Card"), ("Accent", "Sunk"),

            // Подпись на кнопке действия и на кнопке удаления
            ("OnAccent", "Accent"), ("OnAccent", "Negative"),

            // Смысловые цвета сумм
            ("Positive", "Paper"), ("Positive", "Card"),
            ("Negative", "Paper"), ("Negative", "Card"),

            // Карточка нарушенного правила: сообщение и обычный текст на её фоне
            ("Negative", "NegativeBackground"), ("Ink", "NegativeBackground"),

            // Выбранная строка справочника настроек. Ink3 на этот фон не ставится:
            // подписи выбранной строки — основной текст и вторичная подпись
            ("Ink", "AccentBackground"), ("Ink2", "AccentBackground")
        ];

        TheoryData<string, string, string> data = [];

        foreach ((string foreground, string background) in pairs)
        {
            data.Add(foreground, background, "Light");
            data.Add(foreground, background, "Dark");
        }

        return data;
    }

    /// <summary>Пара «текст на фоне» различима по WCAG AA.</summary>
    /// <param name="foreground">Токен текста без суффикса темы.</param>
    /// <param name="background">Токен фона без суффикса темы.</param>
    /// <param name="theme">Суффикс темы: <c>Light</c> или <c>Dark</c>.</param>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Текст_различим_на_своём_фоне(string foreground, string background, string theme)
    {
        double ratio = Ratio(foreground + theme, background + theme);

        Assert.True(
            ratio >= Minimum,
            $"{foreground}{theme} на {background}{theme}: контраст {ratio:F2}, нужен не ниже {Minimum:F1}");
    }

    /// <summary>
    /// Палитра разобрана, а счёт контраста не вырожден. Ошибись разбор — проверка
    /// выше прошла бы на пустом наборе пар и не заметила бы ни одной бледной подписи.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.InRange(Palette.Value.Count, 20, 40);

        // Цвет на себе самом даёт единицу, порядок пары на счёт не влияет
        Assert.Equal(1.0, Ratio("InkLight", "InkLight"), precision: 2);
        Assert.Equal(Ratio("InkLight", "CardLight"), Ratio("CardLight", "InkLight"), precision: 6);

        // Три уровня текста идут по убыванию: разбор не перепутал токены местами
        Assert.True(Ratio("InkLight", "CardLight") > Ratio("Ink2Light", "CardLight"));
        Assert.True(Ratio("Ink2Light", "CardLight") > Ratio("Ink3Light", "CardLight"));
    }

    /// <summary>Цвет палитры: <c>&lt;Color x:Key="Имя"&gt;#RRGGBB&lt;/Color&gt;</c>.</summary>
    [GeneratedRegex("""<Color\s+x:Key="(?<key>\w+)"\s*>#(?<value>[0-9A-Fa-f]{6})</Color>""")]
    private static partial Regex Token();

    private static double Ratio(string foreground, string background)
    {
        double bright = Luminance(foreground);
        double dim = Luminance(background);

        return bright < dim
            ? (dim + 0.05) / (bright + 0.05)
            : (bright + 0.05) / (dim + 0.05);
    }

    private static double Luminance(string token)
    {
        Colour colour = Palette.Value.TryGetValue(token, out Colour found)
            ? found
            : throw new InvalidOperationException($"В палитре нет токена {token}");

        return (0.2126 * Channel(colour.Red)) + (0.7152 * Channel(colour.Green)) + (0.0722 * Channel(colour.Blue));
    }

    // Гамма-коррекция канала по определению относительной яркости WCAG
    private static double Channel(byte value)
    {
        double part = value / 255.0;

        return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
    }

    private static IReadOnlyDictionary<string, Colour> Read()
    {
        string file = Path.Combine(Repository.Root, "src", "Finance.App", "Resources", "Styles", "Colors.xaml");

        return Token().Matches(File.ReadAllText(file))
            .ToDictionary(
                static match => match.Groups["key"].Value,
                static match => Colour.Parse(match.Groups["value"].ValueSpan),
                StringComparer.Ordinal);
    }

    /// <summary>Цвет палитры по каналам.</summary>
    private readonly record struct Colour(byte Red, byte Green, byte Blue)
    {
        /// <summary>Разбирает шесть шестнадцатеричных знаков без решётки.</summary>
        public static Colour Parse(ReadOnlySpan<char> value) =>
            new(
                byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
