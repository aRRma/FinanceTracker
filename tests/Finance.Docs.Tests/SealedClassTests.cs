using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Классы и записи запечатаны, если не задуманы базой: <c>sealed</c> по умолчанию
/// бесплатен по читаемости и снимает с вызовов виртуальную диспетчеризацию.
/// </summary>
/// <remarks>
/// Проверка текстовая: <c>Finance.App</c> собирается только под Android, и анализатор
/// <c>dotnet test</c> его не видит. Базовый класс объявляется <c>abstract</c> —
/// незапечатанный неабстрактный класс здесь всегда забытый <c>sealed</c>.
/// </remarks>
public sealed partial class SealedClassTests
{
    /// <summary>
    /// Миграции EF пишет <c>dotnet ef</c> своим шаблоном: следующая пришла бы
    /// незапечатанной, а замороженные миграции руками не правятся.
    /// </summary>
    private const string Migrations = "/Infrastructure/Storage/Migrations/";

    [Fact]
    public void Классы_запечатаны_если_не_задуманы_базой()
    {
        string[] found = Lines()
            .Where(static line => Unsealed().IsMatch(line.Text))
            .Select(static line => line.ToString())
            .ToArray();

        Assert.True(
            found.Length is 0,
            $"класс без sealed, static или abstract — допишите sealed или объявите базу abstract:\n{string.Join('\n', found)}");
    }

    /// <summary>
    /// Разбор что-то нашёл и на подложенных строках срабатывает. Ошибись выражение —
    /// проверка выше прошла бы на пустом множестве и перестала бы что-либо значить.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(Lines());

        Assert.Matches(Unsealed(), "public partial class FeedPage : DataPage");
        Assert.Matches(Unsealed(), "    private record Sides(int Kind);");
        Assert.DoesNotMatch(Unsealed(), "public sealed partial class FeedPage : DataPage");
        Assert.DoesNotMatch(Unsealed(), "public abstract class DataPage : ContentPage");
        Assert.DoesNotMatch(Unsealed(), "internal static class Faults");
        Assert.DoesNotMatch(Unsealed(), "public readonly record struct Money");
        Assert.DoesNotMatch(Unsealed(), "/// public class в комментарии");
    }

    private static IReadOnlyList<DocumentLine> Lines() =>
        Documents.Lines(Documents.Files("*.cs")
                .Where(static file => !Documents.Relative(file).Contains(Migrations, StringComparison.Ordinal)))
            .ToArray();

    /// <summary>
    /// Объявление класса или записи, перед которым только модификаторы, не запечатывающие
    /// его: доступ, <c>partial</c>, <c>file</c>. Запись-структура запечатана сама.
    /// </summary>
    [GeneratedRegex(@"^\s*(?:(?:public|internal|private|protected|file|partial|unsafe|new)\s+)*(?:class|record)(?!\s+struct\b)\s+\w")]
    private static partial Regex Unsealed();
}
