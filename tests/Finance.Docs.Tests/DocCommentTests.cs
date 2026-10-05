using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// XML-комментарии держат один вид: <c>&lt;summary&gt;</c> и <c>&lt;remarks&gt;</c> — тегами
/// на своих строках, остальные теги — одной строкой. Иначе дописать второе предложение
/// значит переписать строку, а длинное пояснение расползается по параметрам.
/// </summary>
/// <remarks>
/// Компилятор вид комментария не сверяет, а на одной дисциплине правило
/// уже расходилось с кодом.
/// </remarks>
public sealed partial class DocCommentTests
{
    [Fact]
    public void Теги_параметров_однострочные()
    {
        string[] found = CommentLines()
            .Where(static line => OpenTag().Match(line.Text) is { Success: true } open
                && !line.Text.Contains($"</{open.Groups[1].Value}>", StringComparison.Ordinal)
                && !line.Text.TrimEnd().EndsWith("/>", StringComparison.Ordinal))
            .Select(static line => line.ToString())
            .ToArray();

        Assert.True(
            found.Length is 0,
            $"<param>, <returns>, <exception> и <typeparam> пишутся одной строкой, пояснение — в <remarks>:\n{string.Join('\n', found)}");
    }

    [Fact]
    public void Описание_и_примечание_в_три_строки()
    {
        string[] found = CommentLines()
            .Where(static line => BlockOnTextLine().IsMatch(line.Text))
            .Select(static line => line.ToString())
            .ToArray();

        Assert.True(
            found.Length is 0,
            $"<summary> и <remarks> открываются и закрываются на своих строках, даже для одной фразы:\n{string.Join('\n', found)}");
    }

    /// <summary>
    /// Комментарии вообще нашлись. Ошибись отбор — проверки выше прошли бы
    /// на пустом множестве и перестали бы что-либо значить.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл() => Assert.NotEmpty(CommentLines());

    private static IReadOnlyList<DocumentLine> CommentLines() =>
        Documents.Lines(Documents.Files("*.cs"))
            .Where(static line => line.Text.TrimStart().StartsWith("///", StringComparison.Ordinal))
            .ToArray();

    [GeneratedRegex(@"<(param|returns|exception|typeparam)\b")]
    private static partial Regex OpenTag();

    // Текст на одной строке с открывающим или закрывающим тегом
    [GeneratedRegex(@"<(summary|remarks)>\s*\S|///\s*[^<\s].*</(summary|remarks)>")]
    private static partial Regex BlockOnTextLine();
}
