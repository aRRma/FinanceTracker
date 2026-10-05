using System.Text.Json;
using System.Xml.Linq;
using Finance.Application.Infrastructure;
using Microsoft.Maui.Graphics;

namespace Finance.Docs.Tests;

/// <summary>
/// Сверка набора ключей значков с их контурами и названиями. Ключи лежат в одном файле,
/// контуры — в другом: ключ без контура нарисовался бы запасным значком молча,
/// и в сетке выбора появились бы неотличимые «прочие».
/// </summary>
public sealed class IconPathTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LoadedPaths = new(LoadPaths);

    /// <summary>
    /// У каждого ключа набора есть контур.
    /// </summary>
    [Fact]
    public void У_каждого_значка_набора_есть_контур()
    {
        string[] missing = Repository.IconKeys
            .Where(key => !LoadedPaths.Value.ContainsKey(key))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>
    /// У каждого значка счёта есть контур. Набор свой, в коде, а не в файле набора
    /// категорий, и сверка файла его не видит.
    /// </summary>
    [Fact]
    public void У_каждого_значка_счёта_есть_контур()
    {
        string[] missing = [.. AccountIcon.Choices.Where(key => !LoadedPaths.Value.ContainsKey(key))];

        Assert.Empty(missing);
    }

    /// <summary>
    /// Названия для озвучки заведены ровно на ключи обоих наборов — категорий и счетов.
    /// Значок без названия озвучка прочла бы латинским ключом, а название удалённого
    /// значка — мёртвая строка.
    /// </summary>
    [Fact]
    public void Названия_значков_совпадают_с_набором()
    {
        HashSet<string> names = XDocument.Load(Path.Combine(Repository.Root, "src/Finance.Application/Texts/IconNames.resx"))
            .Root!
            .Elements("data")
            .Select(static element => element.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> keys = [.. Repository.IconKeys, .. AccountIcon.Choices];

        string[] unnamed = [.. keys.Where(key => !names.Contains(key))];
        string[] stale = [.. names.Where(name => !keys.Contains(name))];

        Assert.True(
            unnamed.Length is 0 && stale.Length is 0,
            $"без названия: {string.Join(", ", unnamed)}\nназвание без значка: {string.Join(", ", stale)}");
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
        Assert.NotEmpty(Repository.IconKeys);
        Assert.True(
            LoadedPaths.Value.Count >= Repository.IconKeys.Count,
            $"контуров {LoadedPaths.Value.Count}, ключей набора {Repository.IconKeys.Count}");
    }

    /// <summary>
    /// Значок перевода нужен ленте, хотя в наборе для категорий его нет.
    /// </summary>
    [Fact]
    public void Контур_перевода_есть() => Assert.True(LoadedPaths.Value.ContainsKey("swap"));

    private static IReadOnlyDictionary<string, string> LoadPaths()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Repository.IconPaths));

        return document.RootElement.GetProperty("icons")
            .EnumerateObject()
            .ToDictionary(static icon => icon.Name, static icon => icon.Value.GetString()!, StringComparer.Ordinal);
    }
}
