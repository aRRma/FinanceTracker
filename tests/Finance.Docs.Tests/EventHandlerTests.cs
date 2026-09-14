namespace Finance.Docs.Tests;

/// <summary>
/// Обработчики событий не объявлены <c>async void</c> напрямую. Невыловленное
/// исключение в <c>async void</c> не всплывает к вызывающему — оно роняет процесс,
/// и окно закрывается молча: ни сборка, ни экран об этом не скажут.
/// </summary>
/// <remarks>
/// Проверка текстовая, потому что ловить нечем иначе: <c>Finance.App</c> собирается
/// только под Android, и <c>dotnet test</c> его не трогает. Сборка на такой
/// обработчик не жалуется ни единым предупреждением.
/// </remarks>
public sealed class EventHandlerTests
{
    private const string Application = "src/Finance.App/";

    /// <summary>Единственное место, где <c>async void</c> разрешён: там он с перехватом.</summary>
    private const string Wrapper = "src/Finance.App/Guarded.cs";

    [Fact]
    public void Обработчики_событий_не_объявлены_async_void()
    {
        string[] found = ApplicationLines()
            .Where(static line => line.Text.Contains("async void", StringComparison.Ordinal))
            .Select(static line => line.ToString())
            .ToArray();

        Assert.True(
            found.Length is 0,
            $"обработчик обязан быть синхронным и звать Guarded.Run — иначе сбой закроет окно молча:\n{string.Join('\n', found)}");
    }

    /// <summary>
    /// Файлы приложения вообще нашлись. Ошибись отбор путём — проверка выше
    /// прошла бы на пустом множестве и перестала бы что-либо значить.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл() => Assert.NotEmpty(ApplicationLines());

    private static IReadOnlyList<DocumentLine> ApplicationLines() =>
        Documents.Lines(Documents.Files("*.cs")
                .Select(Documents.Relative)
                .Where(static path => path.StartsWith(Application, StringComparison.Ordinal) && path != Wrapper)
                .Select(static path => Path.Combine(Repository.Root, path)))
            .ToArray();
}
