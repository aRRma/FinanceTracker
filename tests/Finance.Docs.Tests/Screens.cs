using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Экраны макетов. Список берётся из самих макетов: перечисли его отдельно —
/// и он разошёлся бы с разметкой при первой же дорисовке экрана.
/// </summary>
internal static partial class Screens
{
    private static readonly Lazy<IReadOnlySet<string>> Drawn = new(static () =>
        Reference().Matches(File.ReadAllText(Documents.Mockups))
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal));

    /// <summary>
    /// Нарисованные экраны: A-01, B-03 и так далее.
    /// </summary>
    public static IReadOnlySet<string> InMockups => Drawn.Value;

    /// <summary>
    /// Обозначение экрана: буква группы и номер.
    /// </summary>
    [GeneratedRegex("""\b[ABCDE]-\d{2}\b""")]
    public static partial Regex Reference();
}
