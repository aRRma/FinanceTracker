using System.Globalization;
using System.Text;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.Data.Sqlite;

namespace Finance.Application.Tests;

/// <summary>
/// Группы «Без категории» на уже установленном приложении. Набор пишется один раз
/// за жизнь установки, поэтому группы третьей версии доставляет миграция, а новую
/// установку заводит сам набор.
/// </summary>
public sealed class UnsortedGroupsMigrationTests
{
    private const string Seeded = "2000-01-01 00:00:00.0000000+00:00";

    private static readonly string[] Unsorted = ["unsorted_exp", "unsorted_exp.other", "unsorted_inc", "unsorted_inc.other"];

    /// <summary>
    /// База со второй версией набора получает обе группы с их подкатегориями под
    /// ключами, выведенными из текстовых, и отметку третьей версии.
    /// </summary>
    [Fact]
    public async Task База_второй_версии_получает_обе_группы()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        await database.MigrateToAsync("MergeServiceGroups");
        await database.ExecuteAsync(Version("2"));

        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        IReadOnlyList<CategoryListItem> categories = await database.Resolve<ICategoriesQuery>().ReadAsync();

        foreach ((string key, CategoryKind kind) in new[] { ("unsorted_exp", CategoryKind.Expense), ("unsorted_inc", CategoryKind.Income) })
        {
            CategoryListItem group = Assert.Single(categories, item => item.Key == Derive(key));
            CategoryListItem only = Assert.Single(categories, item => item.ParentKey == group.Key);

            Assert.Equal(Preset.Embedded().Groups.Single(item => item.Key == key).Name, group.Name);
            Assert.Equal(kind, group.Kind);
            Assert.False(group.AcceptsAnyKind);
            Assert.Equal(Derive($"{key}.other"), only.Key);
            Assert.Equal(CategoryRole.Other, only.Role);
            Assert.Equal(group.Name, only.Name);
        }

        Assert.Equal("3", await database.Resolve<ILocalSettings>().GetAsync(SettingName.PresetVersion));
    }

    /// <summary>
    /// Обновлённая база получает строки, неотличимые от строк новой установки, —
    /// поле в поле: значок, роль, вид, «вне отчётов» и давняя метка набора. Запрос
    /// категорий подставляет приёмнику вид от группы и разницы бы не показал,
    /// поэтому сверяются сами строки таблицы.
    /// </summary>
    [Fact]
    public async Task Миграция_заводит_те_же_строки_что_набор()
    {
        await using TestDatabase migrated = TestDatabase.CreateUnprepared();
        await migrated.MigrateToAsync("MergeServiceGroups");
        await migrated.ExecuteAsync(Version("2"));
        await migrated.Resolve<DatabaseBootstrapper>().InitializeAsync();

        await using TestDatabase fresh = await TestDatabase.CreateWithPresetAsync();

        string expected = await RowsAsync(fresh);

        Assert.Equal(Unsorted.Length, expected.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(expected, await RowsAsync(migrated));
    }

    /// <summary>
    /// Своя группа пользователя с тем же именем в том же виде остаётся единственной:
    /// имя группы уникально в своём виде без учёта регистра, и две одноимённые
    /// запретили бы сохранить любую из них. Доходная заводится как обычно. База первой
    /// версии отметку не меняет — пересмотра второй версии в ней по-прежнему нет.
    /// </summary>
    [Fact]
    public async Task Занятое_имя_не_дублируется_а_первая_версия_остаётся_первой()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        PresetGroup expense = Preset.Embedded().Groups.Single(group => group.Key == "unsorted_exp");
        Guid own = Keys.New();

        // Своё имя — вразнобой заглавными и строчными, с табуляцией по краям:
        // доменная сверка имён сочла бы его тем же, и миграция обязана тоже
        string ownName = "\t" + Alternating(expense.Name) + " ";

        await database.MigrateToAsync("MergeServiceGroups");
        await database.ExecuteAsync(
            $"""
            {Version("1")}

            INSERT INTO categories
                (key, parent_key, kind, accepts_any_kind, name, icon, role, exclude_from_reports,
                 created_at_utc, updated_at_utc)
            VALUES
                ('{own}', NULL, 'Expense', 1, '{ownName}', 'dots', 'Normal', 0, '{Seeded}', '{Seeded}');
            """);

        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        IReadOnlyList<CategoryListItem> categories = await database.Resolve<ICategoriesQuery>().ReadAsync();

        Assert.DoesNotContain(categories, item => item.Key == Derive("unsorted_exp"));
        Assert.DoesNotContain(categories, item => item.Key == Derive("unsorted_exp.other"));
        Assert.Contains(categories, item => item.Key == Derive("unsorted_inc.other"));

        Assert.Equal("1", await database.Resolve<ILocalSettings>().GetAsync(SettingName.PresetVersion));
    }

    /// <summary>
    /// Откат оставляет группы — в них могут лежать операции — и возвращает только
    /// отметку версии; повторный подъём групп не дублирует и отметку поднимает снова.
    /// </summary>
    [Fact]
    public async Task Откат_оставляет_группы_а_повторный_подъём_их_не_дублирует()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        await database.MigrateToAsync("MergeServiceGroups");
        await database.ExecuteAsync(Version("2"));
        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        await database.MigrateToAsync("MergeServiceGroups");

        Assert.Equal("2", await database.Resolve<ILocalSettings>().GetAsync(SettingName.PresetVersion));

        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        IReadOnlyList<CategoryListItem> categories = await database.Resolve<ICategoriesQuery>().ReadAsync();

        Assert.Single(categories, item => item.Key == Derive("unsorted_exp"));
        Assert.Single(categories, item => item.Key == Derive("unsorted_inc.other"));
        Assert.Equal("3", await database.Resolve<ILocalSettings>().GetAsync(SettingName.PresetVersion));
    }

    /// <summary>
    /// На новой установке миграция идёт раньше набора и ничего не вставляет: набор
    /// заводит группы сам, по одной каждого вида, и не падает на повторном ключе.
    /// </summary>
    [Fact]
    public async Task Новая_установка_получает_группы_из_набора_по_одной()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        IReadOnlyList<CategoryListItem> categories = await database.Resolve<ICategoriesQuery>().ReadAsync();

        Assert.Single(categories, item => item.Key == Derive("unsorted_exp"));
        Assert.Single(categories, item => item.Key == Derive("unsorted_inc"));
        Assert.Equal("3", await database.Resolve<ILocalSettings>().GetAsync(SettingName.PresetVersion));
    }

    private static Guid Derive(string key) => Keys.Derive(Preset.Embedded().Namespace, key);

    // Строка отметки версии — так её оставил набор прежней версии
    private static string Version(string value) =>
        $"INSERT INTO settings (name, value) VALUES ('{SettingName.PresetVersion}', '{value}');";

    // Буквы через одну заглавными: ни одно из привычных написаний, а для домена — то же имя
    private static string Alternating(string name) =>
        string.Create(name.Length, name, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                span[i] = i % 2 == 0
                    ? char.ToUpper(source[i], CultureInfo.InvariantCulture)
                    : char.ToLower(source[i], CultureInfo.InvariantCulture);
            }
        });

    // Строки групп «Без категории» целиком, все колонки, по ключу: так видно любое поле,
    // в котором миграция разошлась бы с набором
    private static async Task<string> RowsAsync(TestDatabase database)
    {
        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT * FROM categories WHERE key IN ({string.Join(", ", Unsorted.Select(static key => $"'{Derive(key)}'"))}) ORDER BY key";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();

        StringBuilder rows = new();

        while (await reader.ReadAsync())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                rows.Append(CultureInfo.InvariantCulture, $"{reader.GetName(i)}={reader.GetValue(i)}; ");
            }

            rows.Append('\n');
        }

        return rows.ToString();
    }
}
