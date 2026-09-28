using System.Text.Json;
using Microsoft.Maui.Graphics;

namespace Finance.Docs.Tests;

/// <summary>
/// Сверка набора ключей значков с их контурами. Ключи лежат в одном файле,
/// контуры — в другом: ключ без контура нарисовался бы запасным значком молча,
/// и в сетке выбора появились бы неотличимые «прочие».
/// </summary>
public sealed class IconPathTests
{
    private static readonly Lazy<IReadOnlyList<string>> LoadedKeys = new(LoadKeys);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> LoadedPaths = new(LoadPaths);

    /// <summary>
    /// У каждого ключа набора есть контур.
    /// </summary>
    [Fact]
    public void У_каждого_значка_набора_есть_контур()
    {
        string[] missing = LoadedKeys.Value
            .Where(key => !LoadedPaths.Value.ContainsKey(key))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Каждый контур разбирается тем же разборщиком, что и в приложении, и не пуст.
    /// Испорченный путь иначе упал бы только при отрисовке, на устройстве.
    /// </summary>
    [Fact]
    public void Каждый_контур_разбирается_и_не_пуст()
    {
        List<string> broken = [];

        foreach ((string key, string data) in LoadedPaths.Value)
        {
            try
            {
                if (PathBuilder.Build(data).Count is 0)
                {
                    broken.Add($"{key}: пустой контур");
                }
            }
            catch (Exception error) when (error is FormatException or InvalidOperationException or ArgumentException)
            {
                broken.Add($"{key}: {error.Message}");
            }
        }

        Assert.Empty(broken);
    }

    /// <summary>
    /// Оба файла разобраны, и контуров не меньше, чем ключей набора. Иначе проверка
    /// контуров выше обошла бы пустой словарь и прошла, не разобрав ни одного.
    /// </summary>
    [Fact]
    public void Разбор_что_то_нашёл()
    {
        Assert.NotEmpty(LoadedKeys.Value);
        Assert.True(
            LoadedPaths.Value.Count >= LoadedKeys.Value.Count,
            $"контуров {LoadedPaths.Value.Count}, ключей набора {LoadedKeys.Value.Count}");
    }

    /// <summary>
    /// Значок перевода нужен ленте, хотя в наборе для категорий его нет.
    /// </summary>
    [Fact]
    public void Контур_перевода_есть() => Assert.True(LoadedPaths.Value.ContainsKey("swap"));

    private static IReadOnlyList<string> LoadKeys()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Repository.Icons));

        return document.RootElement.GetProperty("icons")
            .EnumerateArray()
            .Select(static key => key.GetString()!)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> LoadPaths()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Repository.IconPaths));

        return document.RootElement.GetProperty("icons")
            .EnumerateObject()
            .ToDictionary(static icon => icon.Name, static icon => icon.Value.GetString()!, StringComparer.Ordinal);
    }
}
