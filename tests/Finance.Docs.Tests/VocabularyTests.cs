using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Документация зовёт каждое понятие тем словом, что записано в словаре проекта.
/// Второе имя у понятия расходится с первым молча: читатель решает, что речь
/// о разных вещах, и заводит под них разный код.
/// </summary>
public sealed class VocabularyTests
{
    /// <summary>
    /// Запрещены только слова, неверные при любом употреблении. Выводить основы
    /// из словаря автоматически пробовали: «обмен», «сумма», «разница», «журнал»
    /// законны в своих смыслах, и проверка утонула в ложных срабатываниях.
    /// </summary>
    /// <remarks>
    /// Список — данные, а не выражение: он растёт правкой строки таблицы. Поэтому
    /// обычный <see cref="Regex"/>, а не <c>[GeneratedRegex]</c> на каждое слово.
    /// </remarks>
    private static readonly (Regex Wrong, string Right)[] Banned =
    [
        (Pattern(@"копилк\w*"), "«Накопления»"),
        (Pattern(@"контрагент\w*"), "«Место»"),
        (Pattern(@"родительск\w+\s+категори\w+"), "«группа»"),
        (Pattern(@"категори\w+\s+первого\s+уровня"), "«группа»"),
        (Pattern(@"категори\w+\s+второго\s+уровня"), "«подкатегория»"),
        (Pattern("payee"), "Place"),
        (Pattern(@"доступн\w+\s+остат\w+"), "«доступно к тратам»"),
        (Pattern(@"истори\w+\s+по\s+счёту"), "«лента счёта»"),
        (Pattern("exclude_from_available"), "excluded_from_totals"),
        (Pattern(@"архивн\w+\s+сч[её]т\w*"), "«заблокированный счёт»"),
        (Pattern(@"\bпресет\w*"), "«стартовый набор»"),
        (Pattern(@"\bсним(ок|ка|ку|ком|ке)\s+базы"), "«резервная копия»"),
        (Pattern(@"\bсрез\w*\s+по\s+функци"), "«слайс»"),
        (Pattern(@"сч[её]т\w*[\s-]+(назначени\w*|источник\w*)"), "«счёт зачисления» или «счёт списания»"),
        (Pattern(@"\bосновн\w+\s+сч[её]т"), "«счёт списания»"),
        (Pattern(@"\bвтор\w+\s+сумм"), "«сумма зачисления»"),
        (Pattern("counter_(account|amount)"), "target_account или target_amount"),
        (Pattern(@"\bзасе[вя]\w*"), "«инициализация базы»"),
    ];

    /// <summary>
    /// Документы зовут понятия словами из словаря.
    /// </summary>
    [Fact]
    public void Запрещённых_слов_в_документации_нет()
    {
        string[] wrong = Documents.Lines(Checked())
            .SelectMany(static line => Banned
                .Select(pair => (line, Found: pair.Wrong.Match(line.Text), pair.Right))
                .Where(static found => found.Found.Success))
            .Select(static found => $"{found.line}: «{found.Found.Value}» — по словарю {found.Right}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(wrong);
    }

    /// <summary>
    /// Проверяемых файлов больше нуля, и словарь с решениями в них не попал:
    /// оба перечисляют запрещённые слова по делу.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        IReadOnlyList<string> examined = Checked().ToArray();

        Assert.NotEmpty(examined);
        Assert.DoesNotContain(examined, static file => Path.GetFileName(file) == "CONTEXT.md");
        Assert.Contains(examined, static file => Path.GetFileName(file) == "CLAUDE.md");
        Assert.Contains(Banned, static pair => pair.Wrong.IsMatch("контрагенты"));
    }

    // Словарь перечисляет запрещённые слова по делу, а решения объясняют, почему
    // от них отказались, — цитата в них законна
    private static IEnumerable<string> Checked() =>
        Documents.MarkdownFiles.Where(static file =>
            Path.GetFileName(file) != "CONTEXT.md"
            && Path.GetDirectoryName(file) != Documents.Decisions);

    private static Regex Pattern(string wrong) => new(wrong, RegexOptions.IgnoreCase);
}
