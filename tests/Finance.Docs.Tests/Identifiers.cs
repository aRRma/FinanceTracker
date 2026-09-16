using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Инвентарь идентификаторов требований, сценариев и решений: где определён каждый.
/// Определение живёт ровно в одном месте — строке таблицы требований, заголовке
/// сценария или имени файла решения. Всё остальное упоминание — ссылка: таблица
/// в непокрытых требованиях перечисляет их, но не заводит.
/// </summary>
internal static partial class Identifiers
{
    /// <summary>
    /// Префиксы требований. Отдельной константой: тот же список нужен ссылке и диапазону.
    /// </summary>
    private const string Requirement = """INV|SYN|SEED|NFR|SYS|TECH|FR-(?:ACC|CAT|TRX|PAY|BAL|LED|RPT|SYN|SET)""";

    private static readonly Lazy<IReadOnlyList<(string Id, DocumentLine Where, bool Annulled)>> Found = new(Collect);

    private static readonly Lazy<IReadOnlySet<string>> Ids =
        new(static () => Definitions.Select(static definition => definition.Id).ToHashSet(StringComparer.Ordinal));

    /// <summary>
    /// Определения подряд, вместе с повторными: словарь съел бы повтор, а повтор —
    /// это два разных требования под одним номером, и его ловит отдельный тест.
    /// </summary>
    public static IReadOnlyList<(string Id, DocumentLine Where, bool Annulled)> Definitions => Found.Value;

    /// <summary>
    /// Определённые идентификаторы: на них и только на них разыменовывается ссылка.
    /// </summary>
    public static IReadOnlySet<string> Known => Ids.Value;

    /// <summary>
    /// Префикс идентификатора: <c>FR-PAY</c> у <c>FR-PAY-02</c>.
    /// </summary>
    public static string PrefixOf(string id) => id[..id.LastIndexOf('-')];

    /// <summary>
    /// Номер идентификатора.
    /// </summary>
    public static int NumberOf(string id) => int.Parse(id[(id.LastIndexOf('-') + 1)..]);

    /// <summary>
    /// Наибольший занятый номер каждого префикса: по нему видно, полон ли диапазон.
    /// </summary>
    public static IReadOnlyDictionary<string, int> LastNumbers() =>
        Definitions
            .GroupBy(static definition => PrefixOf(definition.Id), StringComparer.Ordinal)
            .ToDictionary(
                static bucket => bucket.Key,
                static bucket => bucket.Max(static definition => NumberOf(definition.Id)),
                StringComparer.Ordinal);

    /// <summary>
    /// Упоминание идентификатора в тексте: и определение, и ссылка.
    /// </summary>
    [GeneratedRegex($$"""\b(?:(?:{{Requirement}}|UC)-\d{2}|ADR-\d{4})\b""")]
    public static partial Regex Reference();

    /// <summary>
    /// Диапазон вида <c>INV-01 … INV-26</c>. Ловится ради заявки «все правила»:
    /// дописанное правило оставляет такую фразу неверной, и она врёт молча.
    /// </summary>
    [GeneratedRegex($$"""\b(?<prefix>(?:{{Requirement}}|UC)-)(?<lo>\d{2})\s*(?:—|–|-|…|\.\.\.)\s*(?:\k<prefix>)?(?<hi>\d{2})\b""")]
    public static partial Regex Range();

    /// <summary>
    /// Строка таблицы требований; аннулированное требование зачёркнуто.
    /// </summary>
    [GeneratedRegex($$"""^\|\s*(?<annulled>~~)?(?<id>(?:{{Requirement}})-\d{2})~?~?\s*\|""")]
    private static partial Regex DefinitionRow();

    /// <summary>
    /// Заголовок сценария: <c>## UC-11 …</c>.
    /// </summary>
    [GeneratedRegex("""^## (?<id>UC-\d{2})\b""")]
    private static partial Regex ScenarioHeading();

    /// <summary>
    /// Имя файла решения: четыре цифры номера и суть решения словами.
    /// </summary>
    [GeneratedRegex("""^(?<number>\d{4})-[a-z0-9-]+\.md$""")]
    private static partial Regex DecisionFile();

    private static IReadOnlyList<(string Id, DocumentLine Where, bool Annulled)> Collect()
    {
        List<(string Id, DocumentLine Where, bool Annulled)> found = [];

        foreach (DocumentLine line in Documents.Lines(DefinitionFiles()))
        {
            Match row = DefinitionRow().Match(line.Text);
            Match heading = ScenarioHeading().Match(line.Text);
            Match definition = row.Success ? row : heading;

            if (definition.Success)
            {
                found.Add((definition.Groups["id"].Value, line, definition.Groups["annulled"].Success));
            }
        }

        // Решение определяется именем файла: один файл — одно решение, таблицы нет
        foreach (string file in Directory.EnumerateFiles(Documents.Decisions, "*.md"))
        {
            Match name = DecisionFile().Match(Path.GetFileName(file));

            if (name.Success)
            {
                found.Add(($"ADR-{name.Groups["number"].Value}", new DocumentLine(Documents.Relative(file), 1, ""), false));
            }
        }

        return found;
    }

    // Требования, сценарии и описание архитектуры заводят идентификаторы; прочие
    // документы только ссылаются
    private static IEnumerable<string> DefinitionFiles() =>
        Documents.MarkdownFiles.Where(static file =>
            Path.GetDirectoryName(file) == Documents.Requirements
            || file == Documents.UseCases
            || Path.GetFileName(file) == "architecture.md");
}
