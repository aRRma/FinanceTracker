using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Finance.Application.Tests;

/// <summary>
/// Слияние служебных групп на уже установленном приложении. Стартовый набор
/// пишется один раз за всю жизнь установки, поэтому доходную «Разницу»,
/// записанную прежней версией, убирает миграция, а не набор.
/// </summary>
public sealed class ServiceGroupMergeTests
{
    private const string Seeded = "2000-01-01 00:00:00.0000000+00:00";

    /// <summary>
    /// Операции доходной «Разницы» переезжают в оставшуюся служебную подкатегорию,
    /// а сама доходная группа уходит под надгробие. Физического удаления нет:
    /// будущий обмен не узнал бы о нём и воскресил бы группу с другого устройства.
    /// </summary>
    [Fact]
    public async Task Доходная_служебная_группа_сливается_с_оставшейся()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        Guid namespaceKey = Preset.Embedded().Namespace;
        Guid group = Keys.Derive(namespaceKey, "service_inc");
        Guid adjustment = Keys.Derive(namespaceKey, "service_inc.adjust");
        Guid survivor = Keys.Derive(namespaceKey, "service_exp.adjust");
        Guid transaction = Keys.New();

        await MigrateToAsync(database, "AcceptsAnyKind");
        await WriteOldRowsAsync(database, group, adjustment, transaction);

        // Слияние приезжает обычным запуском приложения, а не отдельной командой
        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        Assert.Equal(
            survivor,
            await context.Transactions
                .Where(row => row.Key == transaction)
                .Select(row => row.CategoryKey)
                .SingleAsync());

        // Глобальный фильтр мягкого удаления прячет обе строки от всего кода разом
        Assert.Empty(await context.Categories.Where(row => row.Key == group || row.Key == adjustment).ToListAsync());

        Assert.Equal(2, await CountTombstonedAsync(database, group, adjustment));
    }

    /// <summary>
    /// После слияния служебная группа одна и принимает оба вида: недостача пишется
    /// расходом, излишек доходом, и обоим место в одной статье.
    /// </summary>
    [Fact]
    public async Task Служебная_группа_остаётся_одна_и_принимает_оба_вида()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        IReadOnlyList<CategoryListItem> categories = await database.Resolve<ICategoriesQuery>().ReadAsync();

        CategoryListItem service = Assert.Single(
            categories,
            item => item.IsGroup && item.Role is CategoryRole.Service);

        Assert.True(service.Accepts(CategoryKind.Expense));
        Assert.True(service.Accepts(CategoryKind.Income));
    }

    private static async Task MigrateToAsync(TestDatabase database, string migration)
    {
        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        await context.GetService<IMigrator>().MigrateAsync(migration);
    }

    /// <summary>
    /// Пишет то, что оставила прежняя версия: доходную служебную группу с её
    /// «Разницей» и операцию в ней. Строки кладутся запросом, а не через модель:
    /// модель знает только нынешний набор, а нужен прежний.
    /// </summary>
    private static async Task WriteOldRowsAsync(
        TestDatabase database, Guid group, Guid adjustment, Guid transaction)
    {
        Guid account = Keys.New();

        await ExecuteAsync(
            database,
            $"""
            INSERT INTO categories
                (key, parent_key, kind, accepts_any_kind, name, icon, role, exclude_from_reports,
                 created_at_utc, updated_at_utc)
            VALUES
                ('{group}', NULL, 'Income', NULL, 'Служебное', 'adjustments', 'Service', 0, '{Seeded}', '{Seeded}'),
                ('{adjustment}', '{group}', NULL, NULL, 'Разница', 'adjustments', 'Service', 1, '{Seeded}', '{Seeded}');

            INSERT INTO accounts
                (key, name, type, currency, opening_balance, opened_on, excluded_from_totals, is_closed,
                 sort_order, created_at_utc, updated_at_utc)
            VALUES
                ('{account}', 'Наличные', 'Cash', 'RUB', 0, '2026-01-01', 0, 0, 0, '{Seeded}', '{Seeded}');

            INSERT INTO transactions
                (key, kind, source_account_key, amount, category_key, occurred_on, created_at_utc, updated_at_utc)
            VALUES
                ('{transaction}', 'Income', '{account}', 15000, '{adjustment}', '2026-09-01', '{Seeded}', '{Seeded}');
            """);
    }

    private static async Task<int> CountTombstonedAsync(TestDatabase database, Guid group, Guid adjustment)
    {
        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT COUNT(*) FROM categories WHERE key IN ('{group}', '{adjustment}') AND deleted_at_utc IS NOT NULL";

        return Convert.ToInt32(await command.ExecuteScalarAsync(), provider: null);
    }

    private static async Task ExecuteAsync(TestDatabase database, string sql)
    {
        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }
}
