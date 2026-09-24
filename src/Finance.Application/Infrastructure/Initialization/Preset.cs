using Finance.Application.Infrastructure;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finance.Application.Infrastructure.Initialization;

/// <summary>
/// Стартовый набор категорий, как он лежит в <c>data/preset.json</c>. Разбирается
/// сразу в доменные перечисления: разойдись имя роли или вида в файле с кодом,
/// это обязано упасть на разборе, а не превратиться в значение по умолчанию.
/// </summary>
/// <param name="PresetVersion">Номер версии набора. Записанный однажды, повторно не применяется.</param>
/// <param name="Namespace">Пространство имён, из которого выводятся ключи. Не меняется никогда.</param>
/// <param name="SeededAtUtc">Заведомо давняя метка изменения строк набора, единая для всех устройств.</param>
/// <param name="Note">Пояснение для того, кто откроет файл. В коде не используется.</param>
/// <param name="Groups">Группы набора вместе с их подкатегориями.</param>
public sealed partial record Preset(
    int PresetVersion,
    Guid Namespace,
    DateTimeOffset SeededAtUtc,
    string Note,
    IReadOnlyList<PresetGroup> Groups)
{
    private const string ResourceName = "Finance.Application.preset.json";

    /// <summary>
    /// Разбирает набор из текста файла.
    /// </summary>
    /// <param name="json">Содержимое <c>preset.json</c>.</param>
    public static Preset Parse(string json) =>
        JsonSerializer.Deserialize(json, PresetJson.Default.Preset)
        ?? throw new InvalidOperationException(Faults.PresetEmpty());

    /// <summary>
    /// Читает набор, вшитый в сборку. Инициализация базы идёт без обращения к сети
    /// и без файлов рядом с приложением: на Android их просто негде положить.
    /// </summary>
    public static Preset Embedded()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                              ?? throw new InvalidOperationException(Faults.ResourceMissing(ResourceName));

        using StreamReader reader = new(stream);

        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// Все категории набора обоих уровней одним списком.
    /// </summary>
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

    // Разбор собирается при компиляции, а не отражением на первом вызове: так он
    // не стоит времени при запуске и переживает обрезку кода в релизе.
    // Незнакомое поле — ошибка, а не мусор к пропуску: опечатка в ключе или
    // забытый после переименования признак иначе прошли бы как «значение
    // по умолчанию», а файл этот правится необратимо. Перечисления читаются
    // по имени без учёта регистра: `expense` из файла находит `Expense`
    [JsonSerializable(typeof(Preset))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UseStringEnumConverter = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true)]
    private sealed partial class PresetJson : JsonSerializerContext;
}
