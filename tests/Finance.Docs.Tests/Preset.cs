using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain;

namespace Finance.Docs.Tests;

/// <summary>
/// Стартовый набор категорий, как он лежит в <c>data/preset.json</c>.
/// Разбирается сразу в доменные перечисления: если имя роли или вида в файле
/// разойдётся с кодом, тест упадёт здесь, а не при инициализации базы у пользователя.
/// </summary>
internal sealed record Preset(
    int PresetVersion,
    Guid Namespace,
    DateTimeOffset SeededAtUtc,
    string Note,
    IReadOnlyList<PresetGroup> Groups)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },

        // Незнакомое поле — ошибка, а не мусор к пропуску: опечатка в ключе или
        // забытый после переименования признак иначе прошли бы как «значение
        // по умолчанию», а файл этот правится необратимо
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static Preset Load()
    {
        string json = File.ReadAllText(Repository.Preset);
        return JsonSerializer.Deserialize<Preset>(json, Options)
               ?? throw new InvalidOperationException("data/preset.json пуст");
    }

    /// <summary>Все категории набора обоих уровней одним списком.</summary>
    public IEnumerable<PresetCategory> All()
    {
        foreach (PresetGroup group in Groups)
        {
            yield return new PresetCategory(group.Key, group.Id, group.Icon, group.Role, group.ExcludeFromReports);

            foreach (PresetSubcategory subcategory in group.Subcategories)
            {
                yield return new PresetCategory(
                    subcategory.Key, subcategory.Id, subcategory.Icon, subcategory.Role, subcategory.ExcludeFromReports);
            }
        }
    }
}

internal sealed record PresetGroup(
    string Key,
    string Name,
    CategoryKind Kind,
    string Icon,
    CategoryRole Role,
    Guid Id,
    IReadOnlyList<PresetSubcategory> Subcategories,
    bool ExcludeFromReports = false);

internal sealed record PresetSubcategory(
    string Key,
    string Name,
    string Icon,
    CategoryRole Role,
    Guid Id,
    bool ExcludeFromReports = false);

/// <summary>Категория набора любого уровня — для проверок, одинаковых для групп и подкатегорий.</summary>
internal sealed record PresetCategory(
    string Key,
    Guid Id,
    string Icon,
    CategoryRole Role,
    bool ExcludeFromReports);
