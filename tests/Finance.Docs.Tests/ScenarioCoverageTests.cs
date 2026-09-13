namespace Finance.Docs.Tests;

/// <summary>
/// Сценарии покрывают собой всё нарисованное и всё обещанное: экран без сценария
/// нарисован неизвестно зачем, требование без сценария написано неизвестно для кого.
/// </summary>
public sealed class ScenarioCoverageTests
{
    private static readonly Lazy<string> Scenarios = new(static () => File.ReadAllText(Documents.UseCases));

    private static readonly Lazy<string> Explained = new(static () => File.ReadAllText(Documents.Uncovered));

    /// <summary>Каждый нарисованный экран участвует хотя бы в одном сценарии.</summary>
    [Fact]
    public void Каждый_экран_участвует_в_сценарии()
    {
        IReadOnlySet<string> used = Screens.Reference().Matches(Scenarios.Value)
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] idle = Screens.InMockups.Except(used).Order(StringComparer.Ordinal).ToArray();

        Assert.Empty(idle);
    }

    /// <summary>
    /// Функциональное требование либо попадает в сценарий, либо объясняется в списке
    /// непокрытых. Список — для требований, которым сценарий не нужен по существу,
    /// а не для тех, которым его поленились написать.
    /// </summary>
    [Fact]
    public void Каждое_функциональное_требование_покрыто_сценарием_или_объяснено()
    {
        IReadOnlySet<string> covered = Identifiers.Reference().Matches(Scenarios.Value)
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlySet<string> explained = Identifiers.Reference().Matches(Explained.Value)
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] orphans = Identifiers.Definitions
            .Where(static definition => definition.Id.StartsWith("FR-", StringComparison.Ordinal))
            // Обмен отложен целиком: сценариев у него не будет до самого обмена
            .Where(static definition => !definition.Id.StartsWith("FR-SYN", StringComparison.Ordinal))
            .Where(static definition => !definition.Annulled)
            .Select(static definition => definition.Id)
            .Where(id => !covered.Contains(id) && !explained.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(orphans);
    }

    /// <summary>
    /// Аннулированные требования разбором найдены. Не найди их разбор — проверка выше
    /// требовала бы сценарий для отменённого требования, и его пришлось бы выдумывать.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(Scenarios.Value);
        Assert.NotEmpty(Explained.Value);
        Assert.Contains(Identifiers.Definitions, static definition => definition.Annulled);
        Assert.Contains(Identifiers.Definitions, static definition => definition.Id.StartsWith("FR-", StringComparison.Ordinal));
    }
}
