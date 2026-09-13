using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Цвет в макетах задаётся только внутри блока палитры. Прямой цвет в разметке
/// экрана переживёт переключение темы и останется светлым на тёмном фоне —
/// то же правило, что и для приложения, где цвета идут токенами темы.
/// </summary>
public sealed partial class PaletteTests
{
    /// <summary>Прямых цветов вне определения палитры в макетах нет.</summary>
    [Fact]
    public void Цвета_живут_только_в_палитре()
    {
        string[] direct = Documents.HtmlFiles
            .SelectMany(OutsidePalette)
            .SelectMany(static line => Color().Matches(line.Text).Select(match => $"{line}: {match.Value}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(direct);
    }

    /// <summary>
    /// Палитра найдена, а разметка вне неё осталась. Ошибись разбор в любую сторону —
    /// проверка либо обошла бы весь файл, либо утонула бы в цветах самой палитры.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        IReadOnlyList<DocumentLine> outside = OutsidePalette(Documents.Mockups).ToArray();

        Assert.NotEmpty(outside);
        Assert.DoesNotContain(outside, static line => line.Text.Contains("--ink:", StringComparison.Ordinal));
        Assert.Contains(outside, static line => line.Text.Contains("<figure", StringComparison.Ordinal));
    }

    /// <summary>Начало блока палитры: <c>:root{…}</c> или <c>[data-theme="dark"]{…}</c>.</summary>
    [GeneratedRegex("""(:root|\[data-theme)[^{]*\{""")]
    private static partial Regex PaletteStart();

    /// <summary>Цвет, записанный значением, а не токеном.</summary>
    [GeneratedRegex("""#[0-9a-fA-F]{3,8}\b|\brgba?\(""")]
    private static partial Regex Color();

    // Вложенность считается по фигурным скобкам: блок палитры кончается там, где
    // закрылся, а не на первой же закрывающей скобке вложенного правила
    private static IEnumerable<DocumentLine> OutsidePalette(string file)
    {
        int depth = 0;

        foreach (DocumentLine line in Documents.Lines(file))
        {
            int change = line.Text.Count(static character => character is '{') - line.Text.Count(static character => character is '}');

            if (depth is 0 && PaletteStart().IsMatch(line.Text))
            {
                depth = change;
                continue;
            }

            if (depth > 0)
            {
                depth += change;
                continue;
            }

            yield return line;
        }
    }
}
