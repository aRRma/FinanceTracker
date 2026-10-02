using System.Text.Json;
using Finance.Application.Infrastructure.Initialization;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Rules;
using Finance.Domain.Values;

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
    private static readonly Lazy<Preset> Loaded = new(
        static () => Preset.Parse(File.ReadAllText(Repository.Preset)));

    private static readonly Lazy<IReadOnlySet<string>> LoadedIcons = new(
        static () => Repository.IconKeys.ToHashSet(StringComparer.Ordinal));

    private static Preset Set => Loaded.Value;

    private static IReadOnlySet<string> Icons => LoadedIcons.Value;

    /// <summary>
    /// Набор разобран и не пуст. Проверка стоит первой намеренно: при сбое разбора
    /// все остальные проверки прошли бы по пустому списку и ничего не заметили.
    /// </summary>
    [Fact]
    public void Набор_разобран_целиком()
    {
        Assert.Equal(14, Set.Groups.Count);
        Assert.Equal(75, Set.Groups.Sum(group => group.Subcategories.Count));
        Assert.Equal(89, Set.All().Count());
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

    /// <summary>
    /// Вшитый в приложение набор — тот же файл, что лежит в репозитории. Отвались
    /// ссылка на него в проекте, приложение молча засеяло бы старый набор,
    /// а проверки здесь прошли бы по файлу и ничего не заметили.
    /// </summary>
    [Fact]
    public void Вшитый_в_приложение_набор_совпадает_с_файлом()
    {
        Preset embedded = Preset.Embedded();

        Assert.Equal(Set.PresetVersion, embedded.PresetVersion);
        Assert.Equal(Set.Namespace, embedded.Namespace);
        Assert.Equal(Set.SeededAtUtc, embedded.SeededAtUtc);
        Assert.Equal(
            Set.All().Select(category => category.Id),
            embedded.All().Select(category => category.Id));
    }

    /// <summary>
    /// Значки берутся только из набора, зашитого в приложение.
    /// </summary>
    [Fact]
    public void Значки_берутся_из_набора()
    {
        string[] outside = Set.All()
            .Where(category => !Icons.Contains(category.Icon))
            .Select(category => $"{category.Key} → {category.Icon}")
            .ToArray();

        Assert.Empty(outside);
    }

    /// <summary>
    /// Метка времени набора заведомо давняя, иначе она затрёт переименования при переустановке.
    /// </summary>
    [Fact]
    public void Метка_времени_набора_заведомо_давняя()
    {
        Assert.True(
            Set.SeededAtUtc < new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            $"seededAtUtc = {Set.SeededAtUtc:O}");
    }

    /// <summary>
    /// Ключи уникальны: из них выводятся идентификаторы, дубль означал бы две категории с одним ключом.
    /// </summary>
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

    /// <summary>
    /// Ключ подкатегории начинается с ключа своей группы — иначе набор нельзя прочитать глазами.
    /// </summary>
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

    /// <summary>
    /// Служебная группа в наборе одна и принимает оба вида: недостача пишется расходом,
    /// излишек доходом, и обоим место в одной статье. Вторая, доходная, существовала
    /// только пока операция обязана была совпадать по виду с группой, и в выборе
    /// дохода показывалась второй строкой «Служебное», неотличимой от первой.
    /// </summary>
    [Fact]
    public void Служебная_группа_в_наборе_одна()
    {
        PresetGroup service = Assert.Single(Set.Groups, group => group.Role == CategoryRole.Service);

        Assert.True(service.AcceptsAnyKind);
    }

    /// <summary>
    /// Расходные группы набора принимают оба вида, доходные — только свой.
    /// Возврат, кэшбэк и правка расхождения ложатся в ту же статью, где лежит
    /// трата, а доход в «Зарплате» расходом не бывает.
    /// </summary>
    /// <remarks>
    /// Исключение одно — расходная «Без категории». Возврата у неразобранной траты
    /// не бывает, а приход «не помню откуда», записанный в неё, вычелся бы из
    /// неразобранного расхода, и отчёт показал бы меньше, чем потрачено.
    /// </remarks>
    [Fact]
    public void Расходные_группы_набора_принимают_оба_вида()
    {
        string[] oneSidedExpenses = Set.Groups
            .Where(static group => group.Kind == CategoryKind.Expense && !group.AcceptsAnyKind)
            .Select(static group => group.Key)
            .ToArray();

        string[] universalIncomes = Set.Groups
            .Where(static group => group.Kind == CategoryKind.Income && group.AcceptsAnyKind)
            .Select(static group => group.Key)
            .ToArray();

        // Равенством, а не отсевом исключения: универсальной «Без категории» стать
        // тоже нельзя, и отсев этого не заметил бы
        Assert.Equal(["unsorted_exp"], oneSidedExpenses);
        Assert.Empty(universalIncomes);
    }

    /// <summary>
    /// Собирает доменную категорию из строки набора, чтобы прогнать её через доменные
    /// правила. Порядок параметров повторяет <see cref="Category.Restore"/>; именем
    /// служит текстовый ключ набора — он и попадёт в текст ошибки.
    /// </summary>
    private static Category AsCategory(
        Guid key, Guid? parentKey, CategoryKind? kind, string textKey, string icon, CategoryRole role) =>
        Category.Restore(
            key, parentKey, kind, acceptsAnyKind: null, textKey, icon, role,
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

    /// <summary>
    /// Разбор строг: незнакомое поле, пропущенное обязательное и незнакомое имя
    /// перечисления роняют загрузку. Настройки разбора живут в атрибуте генератора,
    /// и выпавшая из него строка превратила бы опечатку в значение по умолчанию молча.
    /// </summary>
    /// <param name="original">Фрагмент настоящего файла.</param>
    /// <param name="spoiled">Чем он заменяется.</param>
    [Theory]
    [InlineData("\"key\": \"food\",", "\"key\": \"food\", \"kidn\": \"expense\",")]
    [InlineData("\"presetVersion\": 3,", "")]
    [InlineData("\"kind\": \"expense\"", "\"kind\": \"expens\"")]
    public void Испорченный_набор_не_разбирается(string original, string spoiled)
    {
        string json = File.ReadAllText(Repository.Preset);
        string broken = json.Replace(original, spoiled, StringComparison.Ordinal);

        Assert.NotEqual(json, broken);
        Assert.Throws<JsonException>(() => Preset.Parse(broken));
    }
}
