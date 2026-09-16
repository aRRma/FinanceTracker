using System.Text.Json;
using System.Text.RegularExpressions;
using Finance.Application.Infrastructure.Initialization;

namespace Finance.Docs.Tests;

/// <summary>
/// Числа, которые документация называет вслух, совпадают с данными: сколько в наборе
/// групп и подкатегорий, сколько значков, сколько экранов. Такое число устаревает
/// при первой же правке данных и продолжает выглядеть достоверно.
/// </summary>
public sealed partial class DataNumberTests
{
    private static readonly Lazy<Preset> Loaded = new(
        static () => Preset.Parse(File.ReadAllText(Repository.Preset)));

    private static readonly Lazy<IReadOnlyList<string>> LoadedIcons = new(LoadIcons);

    /// <summary>
    /// Числа документации сходятся с данными.
    /// </summary>
    [Fact]
    public void Числа_в_документации_совпадают_с_данными()
    {
        (Regex Pattern, int Expected, string What)[] checks =
        [
            (Groups(), Loaded.Value.Groups.Count, "групп"),
            (Subcategories(), Loaded.Value.Groups.Sum(static group => group.Subcategories.Count), "подкатегорий"),
            (Icons(), LoadedIcons.Value.Count, "значков"),
            (Keys(), LoadedIcons.Value.Count, "ключей значков"),
            (ScreenCount(), Screens.InMockups.Count, "экранов"),
        ];

        string[] stale = Documents.Lines(Documents.All())
            .Where(static line => !line.Text.Contains("lint:ignore", StringComparison.Ordinal))
            .SelectMany(line => checks
                .SelectMany(check => check.Pattern.Matches(Tags().Replace(line.Text, " "))
                    .Where(match => int.Parse(match.Groups["count"].Value) != check.Expected)
                    .Select(match => $"{line}: «{match.Value.Trim()}» — по данным {check.Expected} {check.What}")))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(stale);
    }

    /// <summary>
    /// Число экранов прописью проверяется тоже: «тридцать экранов» устаревает так же,
    /// как «30 экранов», а цифру в нём не найти.
    /// </summary>
    [Fact]
    public void Число_экранов_прописью_совпадает_с_макетами()
    {
        (string Word, int Value)[] words =
        [
            ("двадцать", 20),
            ("двадцать два", 22),
            ("двадцать две", 22),
            ("тринадцать", 13),
        ];

        string[] stale = Documents.Lines(Documents.All())
            .Where(static line => !line.Text.Contains("lint:ignore", StringComparison.Ordinal))
            .SelectMany(line => words
                .Where(word => word.Value != Screens.InMockups.Count
                    && Regex.IsMatch(Tags().Replace(line.Text, " "), $@"\b{word.Word}\s+экран", RegexOptions.IgnoreCase))
                .Select(word => $"{line}: «{word.Word} экранов» — фактически {Screens.InMockups.Count}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(stale);
    }

    /// <summary>
    /// Ключи значков уникальны: дубль означал бы, что один из них недостижим.
    /// </summary>
    [Fact]
    public void Ключи_значков_не_повторяются()
    {
        string[] duplicates = LoadedIcons.Value
            .GroupBy(static icon => icon, StringComparer.Ordinal)
            .Where(static bucket => bucket.Count() > 1)
            .Select(static bucket => bucket.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Данные прочитаны и выражения что-то находят: на нулевых ожиданиях проверка
    /// выше прошла бы, ничего не сверив.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(Loaded.Value.Groups);
        Assert.NotEmpty(LoadedIcons.Value);
        Assert.NotEmpty(Screens.InMockups);
        Assert.Matches(Groups(), "13 групп");
        Assert.Matches(ScreenCount(), "30 экранов");
        Assert.False(Groups().IsMatch("ADR-13 групп"), "хвост идентификатора не число документации");
    }

    /// <summary>
    /// Разметку HTML перед сверкой чисел убираем: в атрибутах полно своих цифр.
    /// </summary>
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"(?<![-\d])(?<count>\d+)\s+групп")]
    private static partial Regex Groups();

    [GeneratedRegex(@"(?<![-\d])(?<count>\d+)\s+подкатегори")]
    private static partial Regex Subcategories();

    [GeneratedRegex(@"(?<![-\d])(?<count>\d+)\s+значк")]
    private static partial Regex Icons();

    [GeneratedRegex(@"(?<![-\d])(?<count>\d+)\s+ключ")]
    private static partial Regex Keys();

    [GeneratedRegex(@"(?<![-\d])(?<count>\d+)\s+экран")]
    private static partial Regex ScreenCount();

    private static IReadOnlyList<string> LoadIcons()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Repository.Icons));

        return document.RootElement.GetProperty("icons")
            .EnumerateArray()
            .Select(static icon => icon.GetString()!)
            .ToArray();
    }
}
