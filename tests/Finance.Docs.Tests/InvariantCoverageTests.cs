using System.Text.RegularExpressions;
using Finance.Domain.Errors;

namespace Finance.Docs.Tests;

/// <summary>
/// Каждое доменное правило описано в требованиях и покрыто тестом. Без этой проверки
/// новое правило дописывается в документ и остаётся без теста или появляется в коде,
/// так и не попав в требования.
/// </summary>
/// <remarks>
/// Связка идёт по имени члена <see cref="Invariant"/>: строка правила в требованиях
/// упоминает его в обратных кавычках, тест — в пометке
/// <c>[Trait("Инвариант", nameof(Invariant.NameUnique))]</c>. Номера правил живут
/// только в документах; здесь они нужны лишь затем, чтобы найти строки таблицы.
/// </remarks>
public sealed partial class InvariantCoverageTests
{
    private static readonly string Requirements = Path.Combine(Repository.Root, "docs", "requirements");

    private static readonly string DomainRequirements = Path.Combine(Requirements, "02-domain.md");

    // Unknown — значение неинициализированной переменной, а не правило: строки в
    // требованиях и теста с пометкой у него быть не может, и обе сверки его пропускают
    private static readonly IReadOnlySet<string> Declared = Enum.GetNames<Invariant>()
        .Where(static name => name != nameof(Invariant.Unknown))
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Каждое_правило_из_требований_названо_в_коде()
    {
        string[] unnamed = RuleRows()
            .Select(static row => (row.Id, Count: MentionPattern().Count(row.Text)))
            .Where(static row => row.Count != 1)
            .Select(static row => $"{row.Id}: имён правила {row.Count}")
            .ToArray();

        Assert.Empty(unnamed);
    }

    [Fact]
    public void Требования_называют_только_существующие_правила()
    {
        string[] unknown = Mentions()
            .Where(static name => !Declared.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unknown);
    }

    [Fact]
    public void Каждое_правило_кода_описано_в_требованиях_ровно_один_раз()
    {
        Dictionary<string, int> counts = Mentions()
            .CountBy(static name => name, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);

        string[] broken = Declared
            .Where(name => counts.GetValueOrDefault(name) != 1)
            .Select(name => $"{name}: упоминаний {counts.GetValueOrDefault(name)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(broken);
    }

    [Fact]
    public void Каждое_правило_покрыто_тестом()
    {
        IReadOnlySet<string> covered = CoveredByTests();

        string[] uncovered = Declared.Except(covered).Order(StringComparer.Ordinal).ToArray();

        Assert.Empty(uncovered);
    }

    [Fact]
    public void Разбор_что_то_нашёл()
    {
        // Все списки строятся разбором текста: опечатка в выражении обнулила бы
        // проверки, и они прошли бы на пустых множествах
        Assert.NotEmpty(RuleRows());
        Assert.NotEmpty(Mentions());
        Assert.NotEmpty(CoveredByTests());
    }

    /// <summary>
    /// Строка правила в таблице требований; аннулированные зачёркнуты и пропускаются.
    /// </summary>
    [GeneratedRegex(@"^\|\s*(?<annulled>~~)?(?<id>INV-\d{2})(?<text>.*)$", RegexOptions.Multiline)]
    private static partial Regex RuleRowPattern();

    /// <summary>
    /// Упоминание правила в документе: <c>`Invariant.NameUnique`</c>.
    /// </summary>
    [GeneratedRegex(@"`Invariant\.(?<name>\w+)`")]
    private static partial Regex MentionPattern();

    /// <summary>
    /// Пометка теста: <c>[Trait("Инвариант", nameof(Invariant.NameUnique))]</c>.
    /// </summary>
    [GeneratedRegex("""Trait\("Инвариант",\s*nameof\(Invariant\.(?<name>\w+)\)\)""")]
    private static partial Regex TraitPattern();

    private static IReadOnlyList<(string Id, string Text)> RuleRows() =>
        RuleRowPattern().Matches(File.ReadAllText(DomainRequirements))
            .Where(static match => !match.Groups["annulled"].Success)
            .Select(static match => (match.Groups["id"].Value, match.Groups["text"].Value))
            .ToArray();

    private static IReadOnlyList<string> Mentions() =>
        Directory.EnumerateFiles(Requirements, "*.md")
            .SelectMany(static file => MentionPattern().Matches(File.ReadAllText(file)))
            .Select(static match => match.Groups["name"].Value)
            .ToArray();

    private static IReadOnlySet<string> CoveredByTests()
    {
        string tests = Path.Combine(Repository.Root, "tests", "Finance.Domain.Tests");
        string obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";

        return Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(obj, StringComparison.Ordinal))
            .SelectMany(static file => TraitPattern().Matches(File.ReadAllText(file)))
            .Select(static match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
