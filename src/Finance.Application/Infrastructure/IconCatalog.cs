using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Набор значков, вшитый в приложение. Из сети ничего не подгружается: приложение
/// офлайн-first, и значок, не нарисовавшийся без сети, — это дыра в интерфейсе там,
/// где никакой сети не требовалось.
/// </summary>
public sealed class IconCatalog
{
    private const string ResourceName = "Finance.Application.icons.json";

    // Набор неизменяем, поэтому один на процесс: его просят и контейнер, и отрисовка
    // значков в приложении, а разбирать ресурс дважды ради одной строки незачем
    private static readonly Lazy<IconCatalog> Shared = new(Parse);

    private readonly FrozenSet<string> _keys;

    private IconCatalog(string fallback, FrozenSet<string> keys)
    {
        Fallback = fallback;
        _keys = keys;
    }

    /// <summary>
    /// Запасной значок. Им рисуется неизвестный ключ — ошибкой это не считается.
    /// </summary>
    public string Fallback { get; }

    /// <summary>
    /// Все ключи набора в порядке файла — так они и показываются при выборе.
    /// </summary>
    public IReadOnlyList<string> Keys { get; private init; } = [];

    /// <summary>
    /// Набор значков, вшитый в сборку. Читается при первом обращении, дальше общий.
    /// </summary>
    public static IconCatalog Embedded() => Shared.Value;

    private static IconCatalog Parse()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                              ?? throw new InvalidOperationException($"Ресурс {ResourceName} не вшит в сборку");

        IconSet set = JsonSerializer.Deserialize<IconSet>(stream, IconSet.Options)
                      ?? throw new InvalidOperationException("Набор значков пуст");

        return new IconCatalog(set.Fallback, set.Icons.ToFrozenSet(StringComparer.Ordinal))
        {
            Keys = set.Icons
        };
    }

    /// <summary>
    /// Возвращает ключ, которым можно рисовать: сам значок, если он в наборе,
    /// иначе запасной.
    /// </summary>
    /// <param name="key">Ключ значка из категории.</param>
    public string Resolve(string? key) =>
        key is not null && _keys.Contains(key) ? key : Fallback;

    /// <summary>
    /// Разбор <c>icons.json</c>.
    /// </summary>
    /// <param name="Fallback">Запасной значок.</param>
    /// <param name="Icons">Ключи набора.</param>
    private sealed record IconSet(string Fallback, IReadOnlyList<string> Icons)
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        };
    }
}
