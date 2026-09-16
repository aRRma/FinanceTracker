namespace Finance.Docs.Tests;

/// <summary>
/// Корпус документации: какие файлы проверяются и как из них берутся строки.
/// Список собирается обходом репозитория, а не перечислением вручную — новый
/// документ обязан попадать под проверки сам, иначе они обойдут его молча.
/// </summary>
internal static class Documents
{
    private static readonly Lazy<IReadOnlyList<string>> Markdown =
        new(static () => Collect(Repository.Root, "*.md"));

    private static readonly Lazy<IReadOnlyList<string>> Html =
        new(static () => Collect(Path.Combine(Repository.Root, "docs", "ui"), "*.html"));

    /// <summary>
    /// Все документы репозитория, включая корневые CLAUDE.md, PLAN.md и CONTEXT.md.
    /// </summary>
    public static IReadOnlyList<string> MarkdownFiles => Markdown.Value;

    /// <summary>
    /// Макеты и прототип — единственные два HTML-документа проекта.
    /// </summary>
    public static IReadOnlyList<string> HtmlFiles => Html.Value;

    /// <summary>
    /// Макеты экранов: источник и для прототипа, и для списка экранов.
    /// </summary>
    public static string Mockups { get; } = Path.Combine(Repository.Root, "docs", "ui", "mockups.html");

    /// <summary>
    /// Прототип, собранный из макетов.
    /// </summary>
    public static string Prototype { get; } = Path.Combine(Repository.Root, "docs", "ui", "prototype.html");

    /// <summary>
    /// Сборщик прототипа: его правка меняет прототип так же, как правка макетов.
    /// </summary>
    public static string Builder { get; } = Path.Combine(Repository.Root, "tools", "build_prototype.py");

    /// <summary>
    /// Сценарии использования.
    /// </summary>
    public static string UseCases { get; } = Path.Combine(Repository.Root, "docs", "use-cases.md");

    /// <summary>
    /// Требования без сценария и причина у каждого.
    /// </summary>
    public static string Uncovered { get; } = Path.Combine(Repository.Root, "docs", "uncovered.md");

    /// <summary>
    /// Каталог требований.
    /// </summary>
    public static string Requirements { get; } = Path.Combine(Repository.Root, "docs", "requirements");

    /// <summary>
    /// Каталог решений: имя файла задаёт идентификатор решения.
    /// </summary>
    public static string Decisions { get; } = Path.Combine(Repository.Root, "docs", "adr");

    /// <summary>
    /// Путь от корня репозитория в прямых слэшах: в таком виде он открывается из вывода теста.
    /// </summary>
    public static string Relative(string file) =>
        Path.GetRelativePath(Repository.Root, file).Replace('\\', '/');

    /// <summary>
    /// Строки файла с их адресами.
    /// </summary>
    public static IEnumerable<DocumentLine> Lines(string file)
    {
        string relative = Relative(file);
        string[] lines = File.ReadAllLines(file);

        for (int i = 0; i < lines.Length; i++)
        {
            yield return new DocumentLine(relative, i + 1, lines[i]);
        }
    }

    /// <summary>
    /// Строки нескольких файлов подряд: большинство проверок идёт по всему корпусу.
    /// </summary>
    public static IEnumerable<DocumentLine> Lines(IEnumerable<string> files) => files.SelectMany(Lines);

    /// <summary>
    /// Оба вида документов вместе.
    /// </summary>
    public static IEnumerable<string> All() => MarkdownFiles.Concat(HtmlFiles);

    /// <summary>
    /// Файлы репозитория по маске, без каталогов сборки.
    /// </summary>
    public static IReadOnlyList<string> Files(string pattern) => Collect(Repository.Root, pattern);

    private static IReadOnlyList<string> Collect(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(static file => !Foreign(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

    // Каталоги сборки и служебные каталоги полны чужих документов: без отсева
    // проверки пошли бы по файлам пакетов и падали бы на чужом тексте
    private static bool Foreign(string file) =>
        file.Split('/', '\\').Any(static part => part is ".git" or "bin" or "obj" or "node_modules");
}
