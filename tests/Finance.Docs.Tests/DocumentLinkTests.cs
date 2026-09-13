using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Ссылки документации ведут туда, где что-то есть: на существующий файл и на
/// нарисованный экран. Битая ссылка не мешает читать документ и потому живёт
/// в нём годами.
/// </summary>
public sealed partial class DocumentLinkTests
{
    private static readonly Lazy<IReadOnlySet<string>> Names = new(static () =>
        new[] { "*.md", "*.html", "*.json", "*.py" }
            .SelectMany(Documents.Files)
            .Select(static file => Path.GetFileName(file)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase));

    /// <summary>Ссылка вида <c>[текст](путь)</c> ведёт на существующий путь.</summary>
    [Fact]
    public void Ссылки_ведут_на_существующие_пути()
    {
        string[] broken = Documents.MarkdownFiles
            .SelectMany(static file => Documents.Lines(file)
                .SelectMany(line => Link().Matches(line.Text).Select(match => (file, line, Target: match.Groups["path"].Value))))
            .Where(static found => !External(found.Target) && !Exists(found.file, found.Target))
            .Select(static found => $"{found.line}: {found.Target}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(broken);
    }

    /// <summary>
    /// Файл, упомянутый прозой в обратных кавычках, тоже существует. Так ловится
    /// след удалённого или переименованного файла: ссылкой его не оформляли,
    /// и проверка ссылок его не увидит.
    /// </summary>
    [Fact]
    public void Упомянутые_прозой_файлы_существуют()
    {
        string[] missing = Documents.Lines(Documents.MarkdownFiles)
            .SelectMany(static line => Mention().Matches(line.Text).Select(match => (line, Name: match.Value.Trim('`'))))
            .Where(static found => !Names.Value.Contains(Path.GetFileName(found.Name)!))
            .Select(static found => $"{found.line}: {found.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>Ссылка на экран ведёт на нарисованный экран: иначе читать её нечем.</summary>
    [Fact]
    public void Ссылки_на_экраны_разыменовываются()
    {
        string[] dangling = Documents.Lines(Documents.MarkdownFiles)
            .SelectMany(static line => Screens.Reference().Matches(line.Text).Select(match => (line, match.Value)))
            .Where(static found => !Screens.InMockups.Contains(found.Value))
            .Select(static found => $"{found.line}: {found.Value}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(dangling);
    }

    /// <summary>Разбор что-то нашёл: пустой список ссылок прошёл бы проверки молча.</summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.Contains(Documents.Lines(Documents.MarkdownFiles), static line => Link().IsMatch(line.Text));
        Assert.Contains(Documents.Lines(Documents.MarkdownFiles), static line => Mention().IsMatch(line.Text));
        Assert.NotEmpty(Screens.InMockups);
    }

    /// <summary>Ссылка Markdown; якорь после <c>#</c> отбрасывается.</summary>
    [GeneratedRegex("""\[[^\]]*\]\((?<path>[^)#]+?)(?:#[^)]*)?\)""")]
    private static partial Regex Link();

    /// <summary>Упоминание файла прозой: <c>`tools/build_prototype.py`</c>.</summary>
    [GeneratedRegex("""`[\w./-]+\.(?:md|html|json|py)`""")]
    private static partial Regex Mention();

    private static bool External(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static bool Exists(string file, string target)
    {
        string path = Path.Combine(Path.GetDirectoryName(file)!, target);

        return File.Exists(path) || Directory.Exists(path);
    }
}
