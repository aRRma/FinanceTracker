using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Finance.Application.Features.Settings.TimeZones;
using Finance.Application.Texts;
using Finance.Domain.Errors;

namespace Finance.Docs.Tests;

/// <summary>
/// Текст, который видит пользователь, лежит в ресурсах, а не в коде. Проверок
/// шесть, и каждая ловит своё: разошедшийся с перечислением ключ правила,
/// разошедшееся со списком поясов название города, забытую в ресурсе строку,
/// неполную тройку счётных форм, литерал, оставшийся в коде мимо ресурса,
/// и разошедшуюся с ним подпись ярлыка.
/// </summary>
public sealed partial class TextResourceTests
{
    /// <summary>
    /// Здесь текст — предмет проверки, а не показ пользователю.
    /// </summary>
    private const string Checks = "tests/";

    /// <summary>
    /// Внутренние сообщения «кухни кода»: пишутся тому, кто читает журнал,
    /// наружу не выходят и в ресурсы не уезжают. И миграция данных набора: её
    /// строки — данные, как в preset.json, и замороженная миграция не смеет
    /// брать их из ресурса, который переведут или перепишут.
    /// </summary>
    private static readonly string[] Faults =
    [
        "src/Finance.Application/Infrastructure/Faults.cs",
        "src/Finance.App/AppFaults.cs",
        "src/Finance.Domain/Errors/DomainFaults.cs",
        "src/Finance.Import/ImportFaults.cs",
        "src/Finance.Application/Infrastructure/Storage/Migrations/20261001130110_AddUnsortedGroups.cs"
    ];

    [Fact]
    public void Ключи_правил_и_перечисление_совпадают()
    {
        HashSet<string> resource = ResourceKeys("src/Finance.Domain/Errors/RuleTexts.resx");

        HashSet<string> members = Enum.GetNames<RuleText>()
            .Where(static name => name is not nameof(RuleText.Unknown))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(
            members.SetEquals(resource),
            $"без текста: {Join(members.Except(resource))}\nбез члена RuleText: {Join(resource.Except(members))}");
    }

    /// <summary>
    /// Названия городов заведены ровно на зоны списка поясов. Зона без названия
    /// подписала бы строку латинским идентификатором, а название выбывшей зоны —
    /// мёртвая строка.
    /// </summary>
    [Fact]
    public void Названия_городов_совпадают_со_списком_поясов()
    {
        HashSet<string> resource = ResourceKeys("src/Finance.Application/Texts/CityNames.resx");
        HashSet<string> zones = [.. TimeZoneCities.Ids];

        Assert.Equal(zones.Count, TimeZoneCities.Ids.Count);
        Assert.True(
            zones.SetEquals(resource),
            $"без названия: {Join(zones.Except(resource))}\nназвание без зоны: {Join(resource.Except(zones))}");
    }

    [Fact]
    public void Каждая_строка_интерфейса_кому_то_нужна()
    {
        HashSet<string> declared = typeof(UiTexts)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(static property => property.Name)
            .Where(static name => name is not (nameof(UiTexts.ResourceManager) or nameof(UiTexts.Culture)))
            .ToHashSet(StringComparer.Ordinal);

        string sources = string.Join('\n', SourceFiles().Select(File.ReadAllText));

        string[] unused = declared
            .Where(name => !sources.Contains($"UiTexts.{name}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Обратную сторону закрывает компилятор: ключ, которого нет в ресурсе,
        // не станет свойством, и обращение к нему не соберётся
        Assert.True(unused.Length is 0, $"строки заведены и никем не показываются:\n{Join(unused)}");
    }

    [Fact]
    public void Счётные_формы_заведены_тройками()
    {
        HashSet<string> keys = ResourceKeys("src/Finance.Application/Texts/UiTexts.resx");
        string[] forms = ["One", "Few", "Many"];

        string[] broken = keys
            .Where(key => forms.Any(form => key.EndsWith(form, StringComparison.Ordinal)))
            .Select(key => key[..^forms.First(form => key.EndsWith(form, StringComparison.Ordinal)).Length])
            .Distinct(StringComparer.Ordinal)
            .Where(stem => forms.Any(form => !keys.Contains(stem + form)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Пропущенная форма молча даёт слово не в том числе: «5 операция»
        Assert.True(broken.Length is 0, $"счётные формы неполны, нужны все три:\n{Join(broken)}");
    }

    [Fact]
    public void Русских_строк_в_коде_нет()
    {
        string[] found = SourceFiles()
            .Where(static file => !Faults.Contains(Documents.Relative(file), StringComparer.Ordinal))
            .SelectMany(Literals)
            .Where(static found => Cyrillic().IsMatch(found.Text))
            .Select(static found => $"{found}: {found.Text}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Списком, а не первым найденным: править их всё равно придётся все
        Assert.True(
            found.Length is 0,
            $"текст мимо ресурсов — заведите ключ в UiTexts.resx:\n{Join(found)}");
    }

    [Fact]
    public void Подписи_ярлыков_совпадают_с_видами_операции()
    {
        Dictionary<string, string> shortcuts =
            XDocument.Load(Path.Combine(Repository.Root, "src/Finance.App/Platforms/Android/Resources/values/strings.xml"))
                .Root!
                .Elements("string")
                .ToDictionary(
                    static element => element.Attribute("name")!.Value,
                    static element => element.Value,
                    StringComparer.Ordinal);

        // Единственное место, где текст заведён дважды: меню ярлыков рисует
        // лаунчер до старта процесса, и ресурсы приложения ему недоступны
        Assert.Equal(UiTexts.KindExpense, shortcuts["shortcut_expense_short"]);
        Assert.Equal(UiTexts.KindIncome, shortcuts["shortcut_income_short"]);
        Assert.Equal(UiTexts.KindTransfer, shortcuts["shortcut_transfer_short"]);
    }

    /// <summary>
    /// Разбор что-то нашёл и на подложенном тексте срабатывает. Ошибись отбор —
    /// проверки выше прошли бы на пустом множестве и перестали бы что-либо значить.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(SourceFiles());
        Assert.NotEmpty(ResourceKeys("src/Finance.Application/Texts/UiTexts.resx"));

        string sample = Path.GetTempFileName();

        try
        {
            File.WriteAllText(
                sample,
                """
                // комментарий с кириллицей не в счёт
                string keep = "latin only";
                string found = "Счета";
                string nested = $"{count} · {Plural.Of(count, "операция", "операции")}";
                string verbatim = @$"{count} {Plural.Of(count, "счёт", "счета")}";
                """);

            string[] literals = Literals(sample)
                .Where(static line => Cyrillic().IsMatch(line.Text))
                .Select(static line => line.Text)
                .ToArray();

            Assert.Equal(
                [
                    "\"Счета\"",
                    "$\"{count} · {Plural.Of(count, \"операция\", \"операции\")}\"",
                    "@$\"{count} {Plural.Of(count, \"счёт\", \"счета\")}\""
                ],
                literals);
        }
        finally
        {
            File.Delete(sample);
        }
    }

    /// <summary>
    /// Имена строк ресурса.
    /// </summary>
    private static HashSet<string> ResourceKeys(string relative) =>
        XDocument.Load(Path.Combine(Repository.Root, relative))
            .Root!
            .Elements("data")
            .Select(static element => element.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Исходники приложения: код и разметка, без проверок и тестовых данных.
    /// </summary>
    private static IReadOnlyList<string> SourceFiles() =>
        Documents.Files("*.cs")
            .Concat(Documents.Files("*.xaml"))
            .Where(static file => Documents.Relative(file).StartsWith("src/", StringComparison.Ordinal))
            .Where(static file => !Documents.Relative(file).StartsWith(Checks, StringComparison.Ordinal))
            .ToArray();

    /// <summary>
    /// Строковые литералы файла: значения атрибутов в разметке, строки в коде.
    /// Комментарии выброшены — в них тот же текст, и они ничего не показывают.
    /// </summary>
    private static IEnumerable<DocumentLine> Literals(string file)
    {
        string text = LineComment().Replace(
            BlockComment().Replace(File.ReadAllText(file), string.Empty),
            string.Empty);
        bool markup = file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
        string relative = Documents.Relative(file);

        Regex pattern = markup ? Attribute() : Literal();

        foreach (Match match in pattern.Matches(text))
        {
            string value = markup ? match.Groups["value"].Value : match.Value;

            // Разметка привязками и ссылками на ресурсы полна: их значения —
            // не текст, а имена членов
            if (markup && value.StartsWith('{'))
            {
                continue;
            }

            yield return new DocumentLine(relative, text.Take(match.Index).Count(c => c is '\n') + 1, value);
        }
    }

    private static string Join(IEnumerable<string> lines) => string.Join('\n', lines);

    [GeneratedRegex(@"\p{IsCyrillic}")]
    private static partial Regex Cyrillic();

    /// <summary>
    /// Блочный комментарий кода и комментарий разметки разом.
    /// </summary>
    [GeneratedRegex(@"/\*.*?\*/|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    /// <summary>
    /// Строка комментария целиком, вместе с отступом.
    /// </summary>
    /// <remarks>
    /// Только строка, начинающаяся с <c>//</c>: хвост после кода отрезать этим же
    /// выражением нельзя — двойной слеш встречается и внутри самих строк.
    /// </remarks>
    [GeneratedRegex(@"^[ \t]*//.*$", RegexOptions.Multiline)]
    private static partial Regex LineComment();

    /// <summary>
    /// Строковый литерал кода, включая экранированные кавычки внутри.
    /// </summary>
    /// <remarks>
    /// Интерполированная строка — первой ветвью и вместе с подстановками: строки
    /// внутри подстановки (<c>$"{Plural.Of(n, "…")}"</c>) иначе разбивали её на пары
    /// кавычек вперекос, и кириллица оказывалась между найденными литералами.
    /// </remarks>
    [GeneratedRegex(@"(?:\$@?|@\$)""(?:[^""{\\\n]|\\.|\{\{|\{[^}\n]*\})*""|""(?:[^""\\\n]|\\.)*""")]
    private static partial Regex Literal();

    /// <summary>
    /// Значение атрибута разметки.
    /// </summary>
    [GeneratedRegex(@"[\w.:]+\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex Attribute();
}
