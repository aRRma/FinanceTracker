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
        (Pattern(@"\bаналитик\w*"), "«отчёт»"),
        (Pattern(@"\bиконо?к\w*"), "«значок»"),
        (Pattern(@"\bтип\w*\s+операци"), "«вид операции»"),
        (Pattern(@"\bзакрыт\w*\s+сч[её]т"), "«заблокированный счёт»"),
        (Pattern(@"\bтаймзон\w*"), "«часовой пояс»"),
        (Pattern(@"\bбэкап\w*"), "«резервная копия»"),
        (Pattern(@"\bкошел(?:[её]к|ьк)\w*"), "«счёт»"),
        (Pattern(@"\bцифров\w+\s+клавиатур"), "«клавиатура суммы»"),
    ];

    /// <summary>
    /// Документы, макеты и тексты интерфейса зовут понятия словами из словаря:
    /// экран, макет и документ говорят об одном и том же одинаково.
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
        Assert.Contains(examined, static file => Path.GetFileName(file) == "UiTexts.resx");
        Assert.Contains(examined, static file => file == Documents.Mockups);
        Assert.Contains(Banned, static pair => pair.Wrong.IsMatch("контрагенты"));
        Assert.Contains(Banned, static pair => pair.Wrong.IsMatch("расчёт аналитики"));
    }

    /// <summary>
    /// Запрет держит словоформы, а не одну начальную: беглая гласная и мягкий знак
    /// («иконок», «кошелька») выпадали из выражения, написанного по именительному падежу.
    /// </summary>
    [Theory]
    [InlineData("иконок")]
    [InlineData("кошелька")]
    [InlineData("кошелёк")]
    [InlineData("аналитики")]
    [InlineData("типы операций")]
    [InlineData("закрытого счета")]
    [InlineData("цифровой клавиатурой")]
    public void Словоформы_запрещённых_слов_ловятся(string wrong) =>
        Assert.Contains(Banned, pair => pair.Wrong.IsMatch(wrong));

    // Словарь перечисляет запрещённые слова по делу, а решения объясняют, почему
    // от них отказались, — цитата в них законна. Прототип собирается из макетов
    // и подписей сборщика и проверяется через них
    private static IEnumerable<string> Checked() =>
        Documents.MarkdownFiles
            .Where(static file =>
                Path.GetFileName(file) != "CONTEXT.md"
                && Path.GetDirectoryName(file) != Documents.Decisions)
            .Append(Documents.Mockups)
            .Append(Documents.Builder)
            .Append(Path.Combine(Repository.Root, "src", "Finance.Application", "Texts", "UiTexts.resx"))
            .Append(Path.Combine(Repository.Root, "src", "Finance.Domain", "Errors", "RuleTexts.resx"));

    private static Regex Pattern(string wrong) => new(wrong, RegexOptions.IgnoreCase);
}
