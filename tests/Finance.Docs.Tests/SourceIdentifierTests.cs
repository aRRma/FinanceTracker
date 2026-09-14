namespace Finance.Docs.Tests;

/// <summary>
/// Номера требований живут только в документах. В коде правило называется словами
/// или членом <c>Invariant</c>: шифр вида <c>INV-24</c> разработчику ничего не говорит,
/// а при аннулировании требования он молча начинает указывать не туда.
/// </summary>
/// <remarks>
/// Идентификаторы экранов (<c>A-01</c>, <c>D-09</c>) под проверку не попадают: их
/// префиксов нет в списке требований, и ссылка на макет из комментария страницы
/// остаётся законной.
/// </remarks>
public sealed class SourceIdentifierTests
{
    /// <summary>
    /// Где номера требований — предмет проверки, а не ссылка на требование.
    /// Разбор идентификаторов и его собственные тесты живут здесь.
    /// </summary>
    private const string Allowed = "tests/Finance.Docs.Tests/";

    [Fact]
    public void Номера_требований_не_попадают_в_код()
    {
        string[] found = SourceLines()
            .SelectMany(static line => Identifiers.Reference().Matches(line.Text).Select(match => $"{line}: {match.Value}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Сообщением, а не голым Assert.Empty: список мест обрезается на первом,
        // а править их всё равно придётся все
        Assert.True(
            found.Length is 0,
            $"номера требований в коде — назовите правило словами или членом Invariant:\n{string.Join('\n', found)}");
    }

    /// <summary>
    /// Список файлов непуст. Ошибись отбор путём — проверка выше прошла бы
    /// на пустом множестве и перестала бы что-либо значить.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(SourceLines());

        // И сам разбор номеров работает: без этого пустой результат выше
        // означал бы сломанное выражение, а не чистый код
        Assert.Matches(Identifiers.Reference(), "предел суммы задан INV-24");
    }

    /// <summary>Строки всех файлов кода репозитория, кроме проверок документации.</summary>
    private static IReadOnlyList<DocumentLine> SourceLines() =>
        Documents.Lines(Documents.Files("*.cs").Where(static file => !Documents.Relative(file).StartsWith(Allowed, StringComparison.Ordinal)))
            .ToArray();
}
