using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>Инициализация базы: стартовый набор пишется один раз и своим временем.</summary>
public sealed class DatabaseInitializerTests
{
    /// <summary>Набор записан целиком и обоими уровнями.</summary>
    [Fact]
    public async Task Стартовый_набор_записывается_при_первом_запуске()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        Preset preset = Preset.Embedded();

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        Assert.Equal(preset.All().Count(), await context.Categories.CountAsync());
        Assert.Equal(preset.Groups.Count, await context.Categories.CountAsync(row => row.ParentKey == null));
    }

    /// <summary>
    /// Метка изменения у строк набора — та, что записана в файле, а не текущее время.
    /// Текущее выглядело бы для будущего обмена свежее пользовательских правок
    /// и затёрло бы их при переустановке приложения.
    /// </summary>
    [Fact]
    public async Task Метка_времени_набора_берётся_из_файла()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        DateTimeOffset seededAt = Preset.Embedded().SeededAtUtc;

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        Assert.All(
            await context.Categories.Select(row => row.UpdatedAtUtc).ToListAsync(),
            stamped => Assert.Equal(seededAt, stamped));
    }

    /// <summary>
    /// Второй запуск набор не переписывает: пользователь вправе переименовать
    /// и удалить что угодно из него, и повторное применение вернуло бы удалённое.
    /// </summary>
    [Fact]
    public async Task Повторный_запуск_не_трогает_переименованную_категорию()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        DatabaseInitializer initializer = database.Resolve<DatabaseInitializer>();

        await initializer.InitializeAsync();

        Guid renamed;

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            CategoryRow category = await context.Categories.FirstAsync(row => row.ParentKey == null);
            renamed = category.Key;
            category.Name = "Моё название";
            await context.SaveChangesAsync();
        }

        await initializer.InitializeAsync();

        await using FinanceDbContext reader = await database.Contexts.CreateDbContextAsync();

        Assert.Equal(Preset.Embedded().All().Count(), await reader.Categories.CountAsync());
        Assert.Equal("Моё название", await reader.Categories.Where(row => row.Key == renamed)
            .Select(row => row.Name).SingleAsync());
    }

    /// <summary>Номер применённой версии сохранён: по нему и решается, применять ли набор.</summary>
    [Fact]
    public async Task Номер_версии_набора_сохраняется()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        string? applied = await database.Resolve<ILocalSettings>().GetAsync(SettingName.PresetVersion);

        Assert.Equal(Preset.Embedded().PresetVersion.ToString(), applied);
    }

    /// <summary>
    /// Категории и отметка о применённой версии пишутся одним сохранением.
    /// Порознь они переживают падение между собой: набор записан, отметки нет,
    /// и следующий запуск вставляет те же детерминированные ключи повторно —
    /// база отказывает по уникальности ключа, и починить это уже нечем.
    /// </summary>
    [Fact]
    public async Task Набор_и_отметка_о_версии_пишутся_вместе()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        // Одно сохранение — одна транзакция: категории и отметка о версии
        // не могут разойтись, и повторный проход набор не вставляет
        Assert.NotEmpty(await context.Categories.ToListAsync());
        Assert.Single(await context.Settings.Where(row => row.Name == SettingName.PresetVersion).ToListAsync());
    }

    /// <summary>Справочник мест при первом запуске пуст и наполняется по мере ввода операций.</summary>
    [Fact]
    public async Task Справочник_мест_после_инициализации_пуст()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        Assert.Empty(await context.Places.ToListAsync());
    }
}
