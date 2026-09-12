using System.Text.Json;
using Finance.Domain;

namespace Finance.Docs.Tests;

/// <summary>
/// Сверка стартового набора с требованиями к нему и с доменными правилами категорий.
/// Переехало из <c>tools/validate_preset.py</c>: держать две реализации одной
/// проверки на двух языках нельзя — они разойдутся.
/// </summary>
public sealed class PresetTests
{
    // Разбор отложен намеренно: в инициализаторе поля сбой пришёл бы как
    // TypeInitializationException во всех тестах разом, а настоящая причина
    // оказалась бы двумя уровнями глубже. Через Lazy падает исходное исключение
    private static readonly Lazy<Preset> Loaded = new(Preset.Load);

    private static readonly Lazy<IReadOnlySet<string>> LoadedIcons = new(LoadIcons);

    private static Preset Set => Loaded.Value;

    private static IReadOnlySet<string> Icons => LoadedIcons.Value;

    /// <summary>
    /// Набор разобран и не пуст. Проверка стоит первой намеренно: при сбое разбора
    /// все остальные проверки прошли бы по пустому списку и ничего не заметили.
    /// </summary>
    [Fact]
    public void Набор_разобран_целиком()
    {
        Assert.Equal(13, Set.Groups.Count);
        Assert.Equal(63, Set.Groups.Sum(group => group.Subcategories.Count));
        Assert.Equal(76, Set.All().Count());
        Assert.NotEqual(Guid.Empty, Set.Namespace);
        Assert.All(Set.All(), category => Assert.NotEqual(Guid.Empty, category.Id));
    }

    /// <summary>
    /// Ключ каждой категории выводится из пространства имён и текстового ключа.
    /// Заодно это единственная боевая проверка собственной реализации UUIDv5 —
    /// в .NET её нет, а ошибка в порядке байтов не видна иначе никак.
    /// </summary>
    [Fact]
    public void Ключи_категорий_выводятся_из_текстовых_ключей()
    {
        string[] broken = Set.All()
            .Where(category => Keys.Derive(Set.Namespace, category.Key) != category.Id)
            .Select(category => category.Key)
            .ToArray();

        Assert.Empty(broken);
    }

    /// <summary>Значки берутся только из набора, зашитого в приложение.</summary>
    [Fact]
    public void Значки_берутся_из_набора()
    {
        string[] outside = Set.All()
            .Where(category => !Icons.Contains(category.Icon))
            .Select(category => $"{category.Key} → {category.Icon}")
            .ToArray();

        Assert.Empty(outside);
    }

    /// <summary>Метка времени набора заведомо давняя, иначе она затрёт переименования при переустановке.</summary>
    [Fact]
    public void Метка_времени_набора_заведомо_давняя()
    {
        Assert.True(
            Set.SeededAtUtc < new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            $"seededAtUtc = {Set.SeededAtUtc:O}");
    }

    /// <summary>Ключи уникальны: из них выводятся идентификаторы, дубль означал бы две категории с одним ключом.</summary>
    [Fact]
    public void Текстовые_ключи_уникальны()
    {
        string[] duplicates = Set.All()
            .GroupBy(category => category.Key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }

    /// <summary>Ключ подкатегории начинается с ключа своей группы — иначе набор нельзя прочитать глазами.</summary>
    [Fact]
    public void Ключ_подкатегории_начинается_с_ключа_группы()
    {
        string[] broken = Set.Groups
            .SelectMany(group => group.Subcategories.Select(subcategory => (group, subcategory)))
            .Where(pair => !pair.subcategory.Key.StartsWith(pair.group.Key + ".", StringComparison.Ordinal))
            .Select(pair => pair.subcategory.Key)
            .ToArray();

        Assert.Empty(broken);
    }

    /// <summary>
    /// У каждой группы ровно один приёмник «Прочее», у служебной — одна
    /// служебная подкатегория. Правило не переписывается здесь заново, а берётся
    /// из домена: две реализации одной проверки разошлись бы при первой же правке.
    /// </summary>
    [Fact]
    public void Состав_подкатегорий_каждой_группы_допустим()
    {
        Assert.All(Set.Groups, group =>
        {
            Category domainGroup = AsCategory(group.Id, null, group.Kind, group.Key, group.Icon, group.Role);
            Category[] subcategories = group.Subcategories
                .Select(s => AsCategory(s.Id, group.Id, null, s.Key, s.Icon, s.Role))
                .ToArray();

            CategoryRules.EnsureHasReceiver(domainGroup, subcategories);
        });
    }

    /// <summary>Служебные группы в наборе есть — иначе проверка выше прошла бы, ничего не проверив.</summary>
    [Fact]
    public void Служебные_группы_в_наборе_присутствуют()
    {
        Assert.Equal(2, Set.Groups.Count(group => group.Role == CategoryRole.Service));
    }

    /// <summary>
    /// Собирает доменную категорию из строки набора, чтобы прогнать её через доменные
    /// правила. Порядок параметров повторяет <see cref="Category.Restore"/>; именем
    /// служит текстовый ключ набора — он и попадёт в текст ошибки.
    /// </summary>
    private static Category AsCategory(
        Guid key, Guid? parentKey, CategoryKind? kind, string textKey, string icon, CategoryRole role) =>
        Category.Restore(
            key, parentKey, kind, textKey, icon, role,
            excludeFromReports: false, Set.SeededAtUtc, Set.SeededAtUtc, null, null, null);

    /// <summary>
    /// Признак исключения из отчётов стоит ровно у служебных подкатегорий.
    /// Проверяются оба уровня: у групп этого признака в наборе нет, и появиться
    /// он там не должен — отчёт отбирает по подкатегориям.
    /// </summary>
    [Fact]
    public void Из_отчётов_исключены_только_служебные_подкатегории()
    {
        string[] broken = Set.Groups
            .SelectMany(group => group.Subcategories)
            .Where(subcategory => subcategory.ExcludeFromReports != (subcategory.Role == CategoryRole.Service))
            .Select(subcategory => subcategory.Key)
            .ToArray();

        Assert.Empty(broken);
        Assert.All(Set.Groups, group => Assert.False(group.ExcludeFromReports, group.Key));
    }

    /// <summary>
    /// Оба вида представлены группами набора. Что вид задан только на первом уровне,
    /// проверяет сам разбор: <c>kind</c> у подкатегории — незнакомое поле,
    /// а незнакомые поля запрещены и роняют загрузку. А вот набор без доходных групп
    /// разобрался бы молча и оставил отчёт о доходах пустым.
    /// </summary>
    [Fact]
    public void Оба_вида_представлены_в_наборе()
    {
        Assert.Contains(Set.Groups, group => group.Kind == CategoryKind.Expense);
        Assert.Contains(Set.Groups, group => group.Kind == CategoryKind.Income);
    }

    private static IReadOnlySet<string> LoadIcons()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Repository.Icons));
        return document.RootElement.GetProperty("icons")
            .EnumerateArray()
            .Select(icon => icon.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
