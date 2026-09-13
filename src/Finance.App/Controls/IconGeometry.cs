using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Finance.Application.Infrastructure;

namespace Finance.App.Controls;

/// <summary>
/// Контуры значков, вшитые в приложение: ключ из набора — путь SVG. Живут в
/// <c>Finance.App</c>, а не рядом с набором ключей: контур нужен только для
/// отрисовки, а модели представления и тесты работают ключом.
/// </summary>
internal static class IconGeometry
{
    private const string ResourceName = "Finance.App.icon-paths.json";

    // Контуры разбираются один раз на приложение. Это образцы: значок отдаёт
    // отрисовке копию, потому что MAUI преобразует путь под границы вида на месте
    private static readonly FrozenDictionary<string, PathF> Paths = Load();

    // Запасной ключ тот же, что у набора: неизвестный ключ рисуется «прочим»,
    // а не пустотой, и ошибкой это не считается
    private static readonly PathF Fallback = Paths[IconCatalog.Embedded().Fallback];

    /// <summary>
    /// Образец контура для ключа. Неизвестный или пустой заменяется запасным.
    /// Отдавать отрисовке напрямую нельзя — только копией, см. <see cref="Icon.GetPath"/>.
    /// </summary>
    /// <param name="key">Ключ значка.</param>
    public static PathF For(string? key) =>
        key is not null && Paths.TryGetValue(key, out PathF? path) ? path : Fallback;

    private static FrozenDictionary<string, PathF> Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                              ?? throw new InvalidOperationException($"Ресурс {ResourceName} не вшит в сборку");

        // Разбор по документу, а не сериализатором: обрезка кода в релизе
        // вырезает то, до чего сериализатор добирается отражением
        using JsonDocument document = JsonDocument.Parse(stream);

        Dictionary<string, PathF> paths = [];

        foreach (JsonProperty icon in document.RootElement.GetProperty("icons").EnumerateObject())
        {
            string data = icon.Value.GetString()
                          ?? throw new InvalidOperationException($"У значка «{icon.Name}» нет контура");

            paths[icon.Name] = PathBuilder.Build(data);
        }

        return paths.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
