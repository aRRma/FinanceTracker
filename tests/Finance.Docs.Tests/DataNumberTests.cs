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
            (Icons(), Repository.IconKeys.Count, "значков"),
            (Keys(), Repository.IconKeys.Count, "ключей значков"),
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
    /// Число экранов прописью проверяется тоже: «тридцать два экрана» устаревает так же,
    /// как «32 экрана», а цифру в нём не найти. Числа меньше десяти не разбираются:
    /// «два экрана» в прозе — почти всегда часть макетов, а не их общее число.
    /// </summary>
    [Fact]
    public void Число_экранов_прописью_совпадает_с_макетами()
    {
        string[] stale = Documents.Lines(Documents.All())
            .Where(static line => !line.Text.Contains("lint:ignore", StringComparison.Ordinal))
            .SelectMany(static line => ScreenCountInWords().Matches(Tags().Replace(line.Text, " "))
                .Where(static match => FromWords(match.Groups["count"].Value) != Screens.InMockups.Count)
                .Select(match => $"{line}: «{match.Value.Trim()}» — фактически {Screens.InMockups.Count}"))
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
        string[] duplicates = Repository.IconKeys
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
        Assert.NotEmpty(Repository.IconKeys);
        Assert.NotEmpty(Screens.InMockups);
        Assert.Matches(Groups(), "13 групп");
        Assert.Matches(ScreenCount(), "30 экранов");
        Assert.DoesNotMatch(ScreenCount(), "09:55:18 экран «Ещё»");
        Assert.False(Groups().IsMatch("ADR-13 групп"), "хвост идентификатора не число документации");
        Assert.Equal(32, FromWords(ScreenCountInWords().Match("Тридцать два экрана").Groups["count"].Value));
        Assert.Equal(13, FromWords(ScreenCountInWords().Match("тринадцать экранов").Groups["count"].Value));
        Assert.DoesNotMatch(ScreenCountInWords(), "два экрана");
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

    // Двоеточие впереди — время, а не счёт: «09:55:18 экран «Ещё»» в следе действий на макете
    [GeneratedRegex(@"(?<![-:\d])(?<count>\d+)\s+экран")]
    private static partial Regex ScreenCount();

    [GeneratedRegex(
        @"(?<!\w)(?<count>(?:двадцать|тридцать|сорок|пятьдесят)(?:\s+(?:один|одна|два|две|три|четыре|пять|шесть|семь|восемь|девять))?|десять|\w+надцать)\s+экран",
        RegexOptions.IgnoreCase)]
    private static partial Regex ScreenCountInWords();

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["один"] = 1, ["одна"] = 1, ["два"] = 2, ["две"] = 2, ["три"] = 3, ["четыре"] = 4,
        ["пять"] = 5, ["шесть"] = 6, ["семь"] = 7, ["восемь"] = 8, ["девять"] = 9,
        ["десять"] = 10, ["одиннадцать"] = 11, ["двенадцать"] = 12, ["тринадцать"] = 13,
        ["четырнадцать"] = 14, ["пятнадцать"] = 15, ["шестнадцать"] = 16, ["семнадцать"] = 17,
        ["восемнадцать"] = 18, ["девятнадцать"] = 19,
        ["двадцать"] = 20, ["тридцать"] = 30, ["сорок"] = 40, ["пятьдесят"] = 50,
    };

    private static int FromWords(string words) =>
        words.Split(' ', StringSplitOptions.RemoveEmptyEntries).Sum(static word => NumberWords[word]);
}
