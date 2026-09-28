using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Время в коде приложения читается только через часы приложения (<c>IClock</c>).
/// Прямое обращение к часам машины обходит зону пользователя — «сегодня» съезжает
/// на сутки каждый вечер — и тестовые часы: тест начинает зависеть от дня запуска.
/// </summary>
/// <remarks>
/// Проверка текстовая: сборка на такое обращение не жалуется, а тесты проходят
/// в любой день, кроме неудачного.
/// </remarks>
public sealed partial class ClockTests
{
    private const string Source = "src/";

    [Fact]
    public void Время_читается_только_через_часы_приложения()
    {
        string[] found = SourceLines()
            .Where(static line => ReadsMachineClock(line.Text))
            .Select(static line => line.ToString())
            .ToArray();

        Assert.True(
            found.Length is 0,
            $"время берётся у IClock, а не у часов машины — иначе мимо зоны пользователя и тестовых часов:\n{string.Join('\n', found)}");
    }

    /// <summary>
    /// Исходники нашлись, а разбор ловит обращение к часам и пропускает его упоминание
    /// в комментарии. Ошибись отбор путём или выражение — проверка выше прошла бы
    /// вхолостую.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл_и_на_подложенном_тексте_срабатывает()
    {
        Assert.NotEmpty(SourceLines());

        Assert.True(ReadsMachineClock("        DateTimeOffset now = DateTimeOffset.UtcNow;"));
        Assert.True(ReadsMachineClock("    DateOnly today = DateOnly.FromDateTime(DateTime.Today);"));
        Assert.False(ReadsMachineClock("/// Отдельный тип, а не обращения к <see cref=\"DateTimeOffset.UtcNow\"/>"));
        Assert.False(ReadsMachineClock("    public DateTimeOffset NowUtc => time.GetUtcNow();"));
    }

    // Комментарии пропускаются: в них часы машины упоминаются как раз затем,
    // чтобы объяснить, почему ими не пользуются
    private static bool ReadsMachineClock(string line) =>
        !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && MachineClock().IsMatch(line);

    private static IReadOnlyList<DocumentLine> SourceLines() =>
        Documents.Lines(Documents.Files("*.cs")
                .Where(static file => Documents.Relative(file).StartsWith(Source, StringComparison.Ordinal)))
            .ToArray();

    [GeneratedRegex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b")]
    private static partial Regex MachineClock();
}
