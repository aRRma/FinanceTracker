using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.Data.Sqlite;

namespace Finance.Application.Tests;

/// <summary>
/// Цвета счетам, заведённым до обновления. Колонку доставляет миграция, она же раздаёт
/// цвета по очереди — та же миграция ждёт и старую выгрузку при восстановлении.
/// </summary>
public sealed class AccountColorMigrationTests
{
    private const string Before = "AddUnsortedGroups";
    private const string Moment = "2026-09-01 10:00:00.0000000+00:00";

    /// <summary>
    /// Действующие получают первые цвета в своём порядке на балансах, заблокированный —
    /// следующий, удалённый — последним; выбранных руками значков ни у кого нет.
    /// </summary>
    [Fact]
    public async Task Цвета_раздаются_по_очереди_действующим_первыми()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();
        await database.MigrateToAsync(Before);

        Guid second = Keys.New();
        Guid first = Keys.New();
        Guid closed = Keys.New();
        Guid deleted = Keys.New();

        await database.ExecuteAsync(
            $"""
            {Insert(second, "Карта", sortOrder: 2)}
            {Insert(first, "Наличные", sortOrder: 0)}
            {Insert(closed, "Старая карта", sortOrder: 1, isClosed: true)}
            {Insert(deleted, "Удалённая", sortOrder: 3, deletedAtUtc: Moment)}
            """);

        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        Dictionary<Guid, (string Color, object Icon)> rows = await RowsAsync(database);

        Assert.Equal("Blue", rows[first].Color);
        Assert.Equal("Orange", rows[second].Color);
        Assert.Equal("Sky", rows[closed].Color);
        Assert.Equal("Green", rows[deleted].Color);
        Assert.All(rows.Values, static row => Assert.Equal(DBNull.Value, row.Icon));

        // Прочитанное приложением — тем же перечислением, а не строкой
        IReadOnlyList<AccountListItem> accounts = await database.Resolve<IAccountsQuery>().ReadAsync();

        Assert.Equal(AccountColor.Blue, accounts.Single(account => account.Key == first).Color);
    }

    /// <summary>
    /// Девятый счёт начинает очередь заново: цветов восемь, и одинаковые разрешены.
    /// </summary>
    [Fact]
    public async Task Девятый_счёт_снова_синий()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();
        await database.MigrateToAsync(Before);

        Guid[] keys = [.. Enumerable.Range(0, 9).Select(static _ => Keys.New())];

        await database.ExecuteAsync(string.Join('\n', keys.Select((key, order) => Insert(key, $"Счёт {order}", order))));

        await database.Resolve<DatabaseBootstrapper>().InitializeAsync();

        Dictionary<Guid, (string Color, object Icon)> rows = await RowsAsync(database);

        Assert.Equal(
            ["Blue", "Orange", "Sky", "Green", "Violet", "Magenta", "Red", "Pink", "Blue"],
            keys.Select(key => rows[key].Color));
    }

    private static string Insert(Guid key, string name, int sortOrder, bool isClosed = false, string? deletedAtUtc = null) =>
        $"""
        INSERT INTO accounts
            (key, name, type, currency, opening_balance, opened_on, excluded_from_totals, is_closed, sort_order,
             created_at_utc, updated_at_utc, deleted_at_utc)
        VALUES
            ('{key}', '{name}', 'Card', 'RUB', 0, '2026-01-01', 0, {(isClosed ? 1 : 0)}, {sortOrder},
             '{Moment}', '{Moment}', {(deletedAtUtc is null ? "NULL" : $"'{deletedAtUtc}'")});
        """;

    // Строки целиком, мимо глобального фильтра мягкого удаления: удалённому цвет тоже нужен
    private static async Task<Dictionary<Guid, (string Color, object Icon)>> RowsAsync(TestDatabase database)
    {
        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT key, color, icon FROM accounts";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();

        Dictionary<Guid, (string, object)> rows = [];

        while (await reader.ReadAsync())
        {
            rows[Guid.Parse(reader.GetString(0))] = (reader.GetString(1), reader.GetValue(2));
        }

        return rows;
    }
}
