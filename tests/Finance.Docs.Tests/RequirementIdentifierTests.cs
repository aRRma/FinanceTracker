namespace Finance.Docs.Tests;

/// <summary>
/// Идентификаторы требований, сценариев и решений держатся вместе: каждый определён
/// один раз и каждая ссылка ведёт к определению. Переехало из проверки документации
/// на Python: держать две реализации одной проверки на двух языках нельзя.
/// </summary>
public sealed class RequirementIdentifierTests
{
    /// <summary>
    /// Повторное определение — два разных требования под одним номером. Ссылка на
    /// такой номер означает то одно, то другое, и расхождение не всплывает никогда.
    /// </summary>
    [Fact]
    public void Каждый_идентификатор_определён_один_раз()
    {
        string[] repeated = Identifiers.Definitions
            .GroupBy(static definition => definition.Id, StringComparer.Ordinal)
            .Where(static bucket => bucket.Count() > 1)
            .Select(static bucket => $"{bucket.Key}: {string.Join(", ", bucket.Select(static d => d.Where))}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(repeated);
    }

    /// <summary>
    /// Префикс живёт в одном файле: разъехавшись по двум, нумерация продолжится
    /// в каждом своя, и следующее требование получит уже занятый номер.
    /// Решений это не касается — там файл на решение.
    /// </summary>
    [Fact]
    public void Префикс_живёт_в_одном_файле()
    {
        string[] spread = Identifiers.Definitions
            .Where(static definition => !definition.Id.StartsWith("ADR-", StringComparison.Ordinal))
            .GroupBy(static definition => Identifiers.PrefixOf(definition.Id), StringComparer.Ordinal)
            .Select(static bucket => (bucket.Key, Files: bucket
                .Select(static definition => definition.Where.File)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()))
            .Where(static bucket => bucket.Files.Length > 1)
            .Select(static bucket => $"{bucket.Key}-*: {string.Join(", ", bucket.Files)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(spread);
    }

    /// <summary>
    /// Пропуск в нумерации означает потерянное требование: номера не освобождаются,
    /// аннулированное остаётся в таблице зачёркнутым.
    /// </summary>
    [Fact]
    public void Номера_идут_без_пропусков()
    {
        string[] gaps = Identifiers.Definitions
            .GroupBy(static definition => Identifiers.PrefixOf(definition.Id), StringComparer.Ordinal)
            .Select(static bucket => (bucket.Key, Taken: bucket
                .Select(static definition => Identifiers.NumberOf(definition.Id))
                .ToHashSet()))
            .Select(static bucket => (bucket.Key, Missing: Enumerable
                .Range(1, bucket.Taken.Max())
                .Where(number => !bucket.Taken.Contains(number))
                .ToArray()))
            .Where(static bucket => bucket.Missing.Length > 0)
            .Select(static bucket => $"{bucket.Key}-*: пропущены {string.Join(", ", bucket.Missing)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(gaps);
    }

    /// <summary>
    /// Ссылка на несуществующий номер — опечатка либо след переименования. Читателя
    /// она отправляет искать требование, которого нет.
    /// </summary>
    [Fact]
    public void Каждая_ссылка_разыменовывается()
    {
        string[] dangling = Documents.Lines(Documents.All())
            .SelectMany(static line => Identifiers.Reference().Matches(line.Text).Select(match => (line, match.Value)))
            .Where(static found => !Identifiers.Known.Contains(found.Value))
            .Select(static found => $"{found.line}: {found.Value}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(dangling);
    }

    /// <summary>
    /// Диапазон, заявленный как полный, обязан доходить до последнего номера:
    /// дописанное требование оставляет фразу «все INV-01…26» неверной молча.
    /// </summary>
    [Fact]
    public void Полный_диапазон_доходит_до_последнего_номера()
    {
        IReadOnlyDictionary<string, int> last = Identifiers.LastNumbers();

        string[] incomplete = Documents.Lines(Documents.MarkdownFiles)
            .Where(static line =>
                line.Text.Contains("все", StringComparison.OrdinalIgnoreCase)
                || line.Text.Contains("кажд", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static line => Identifiers.Range().Matches(line.Text).Select(match => (line, match)))
            .Select(found => (
                found.line,
                Prefix: found.match.Groups["prefix"].Value.TrimEnd('-'),
                Lo: int.Parse(found.match.Groups["lo"].Value),
                Hi: int.Parse(found.match.Groups["hi"].Value)))
            .Where(found => found.Lo is 1 && found.Hi < last.GetValueOrDefault(found.Prefix))
            .Select(found => $"{found.line}: {found.Prefix}-{found.Lo:00}…{found.Hi:00}, "
                + $"а существует до {last[found.Prefix]:00}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(incomplete);
    }

    /// <summary>
    /// Всё выше строится разбором текста: опечатка в выражении обнулила бы проверки,
    /// и они прошли бы на пустых множествах.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(Identifiers.Definitions);
        Assert.Contains("ADR-0001", Identifiers.Known);
        Assert.Contains("UC-01", Identifiers.Known);
        Assert.Contains("INV-01", Identifiers.Known);
        Assert.NotEmpty(Documents.MarkdownFiles);
        Assert.Equal(2, Documents.HtmlFiles.Count);
    }
}
