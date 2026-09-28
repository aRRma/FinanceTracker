using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Навыки под каталогом с настройками Claude Code: правила, вынесенные из CLAUDE.md,
/// обязаны оставаться достижимыми. Навык, на который никто не ссылается, не загрузится,
/// а правило внутри него молча перестанет действовать — сборка и тесты об этом не скажут.
/// </summary>
public sealed partial class SkillTests
{
    // Сорок знаков — примерно пять-шесть слов: меньше не хватает, чтобы назвать,
    // когда навык загружать, и описание вида «сборка» не срабатывает
    private const int MinimumDescription = 40;

    private static readonly Lazy<IReadOnlyList<string>> Loaded = new(Collect);

    private static readonly Lazy<string> LoadedInstructions = new(
        static () => File.ReadAllText(Path.Combine(Repository.Root, "CLAUDE.md")));

    private static IReadOnlyList<string> Files => Loaded.Value;

    private static string Instructions => LoadedInstructions.Value;

    /// <summary>
    /// Каждый навык назван в CLAUDE.md: иначе о нём некому вспомнить.
    /// </summary>
    [Fact]
    public void Каждый_навык_упомянут_в_главном_файле()
    {
        string[] forgotten = Files
            .Select(Folder)
            .Where(static name => !Instructions.Contains($"`{name}`", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            forgotten.Length is 0,
            $"навык есть, а ссылки на него в CLAUDE.md нет:\n{string.Join('\n', forgotten)}");
    }

    /// <summary>
    /// Упоминание навыка разыменовывается в существующую папку: опечатка
    /// и переименование иначе оставляют указатель в никуда.
    /// </summary>
    [Fact]
    public void Упоминания_навыков_разыменовываются()
    {
        HashSet<string> known = [.. Files.Select(Folder)];

        string[] dangling = Mention()
            .Matches(Instructions)
            .Select(static match => match.Groups["name"].Value)
            .Where(name => !known.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            dangling.Length is 0,
            $"CLAUDE.md зовёт навык, которого нет:\n{string.Join('\n', dangling)}");
    }

    /// <summary>
    /// Имя во вступлении навыка совпадает с именем папки, а описание не пустует:
    /// по описанию и решается, загружать ли навык, и «здесь про сборку» не срабатывает.
    /// </summary>
    [Fact]
    public void Вступление_навыка_заполнено()
    {
        string[] broken = Files
            .Where(static file => Name(file) != Folder(file) || Description(file).Length < MinimumDescription)
            .Select(Documents.Relative)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            broken.Length is 0,
            $"имя навыка расходится с папкой или описание короче сорока знаков:\n{string.Join('\n', broken)}");
    }

    /// <summary>
    /// Разбор что-то нашёл: на пустых списках три проверки выше прошли бы молча.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(Files);
        Assert.Matches(Mention(), Instructions);
    }

    private static IReadOnlyList<string> Collect()
    {
        string root = Path.Combine(Repository.Root, ".claude", "skills");

        return Directory.Exists(root)
            ? [.. Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal)]
            : [];
    }

    private static string Folder(string file) => Path.GetFileName(Path.GetDirectoryName(file))!;

    private static string Name(string file) => Field(file, "name");

    private static string Description(string file) => Field(file, "description");

    /// <summary>
    /// Значение поля вступления. Вступление простое — по строке на поле,
    /// и разбирать его полноценным разбором YAML незачем.
    /// </summary>
    private static string Field(string file, string field)
    {
        foreach (string line in File.ReadLines(file).Skip(1).Take(10))
        {
            if (line.StartsWith($"{field}:", StringComparison.Ordinal))
            {
                return line[(field.Length + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    [GeneratedRegex("""навык[а-я]*\s+`(?<name>[a-z][a-z-]*)`""")]
    private static partial Regex Mention();
}
