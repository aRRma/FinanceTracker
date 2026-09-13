using System.Globalization;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Как схема лежит в файле: типы колонок, канонический вид ключа, отсечение
/// мягко удалённых и единая точка простановки метки изменения.
/// </summary>
public sealed class StorageTests
{
    private static readonly DateOnly Today = new(2026, 8, 25);
    private static readonly DateTimeOffset NowUtc = new(2026, 8, 25, 9, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Деньги лежат целыми копейками в INTEGER. Если они уедут в TEXT, по ним
    /// перестанут работать SUM и сравнение, и баланс придётся считать в памяти.
    /// </summary>
    [Fact]
    public async Task Деньги_хранятся_целыми_копейками()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Account account = Account.Create(
            "Наличные", AccountType.Cash, Currency.RUB, 1234.56m, new DateOnly(2026, 1, 1),
            excludedFromTotals: false, sortOrder: 0, Today, NowUtc);

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            context.Accounts.Add(account.ToRow());
            await context.SaveChangesAsync();
        }

        Assert.Equal("INTEGER", await ColumnTypeAsync(database, "accounts", "opening_balance"));
        Assert.Equal(123_456L, await ScalarAsync<long>(database, "SELECT opening_balance FROM accounts"));
    }

    /// <summary>Суммы складываются запросом к базе — иначе баланс потребовал бы поднять всю ленту.</summary>
    [Fact]
    public async Task Суммы_складываются_на_стороне_базы()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Account account = await GivenAccountAsync(database);

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            context.Transactions.Add(GivenExpense(account, 100.50m).ToRow());
            context.Transactions.Add(GivenExpense(account, 0.05m).ToRow());
            await context.SaveChangesAsync();
        }

        await using FinanceDbContext reader = await database.Contexts.CreateDbContextAsync();
        decimal total = await reader.Transactions.SumAsync(row => row.Amount);

        Assert.Equal(100.55m, total);
    }

    /// <summary>
    /// Ключ лежит текстом в каноническом виде. Двоичное хранение переставило бы
    /// байты UUIDv7 и лишило бы ленту последнего разрывателя ничьей.
    /// </summary>
    [Fact]
    public async Task Ключ_хранится_текстом_в_каноническом_виде()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Account account = await GivenAccountAsync(database);

        Assert.Equal("TEXT", await ColumnTypeAsync(database, "accounts", "key"));
        Assert.Equal(
            account.Key.ToString("D"),
            await ScalarAsync<string>(database, "SELECT key FROM accounts"));
    }

    /// <summary>Календарная дата лежит без времени и зоны: иначе граница месяца уедет на сутки.</summary>
    [Fact]
    public async Task Дата_операции_хранится_без_времени()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Account account = await GivenAccountAsync(database);

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            context.Transactions.Add(GivenExpense(account, 10m).ToRow());
            await context.SaveChangesAsync();
        }

        Assert.Equal("2026-08-25", await ScalarAsync<string>(database, "SELECT occurred_on FROM transactions"));
    }

    /// <summary>Мягко удалённое исчезает с экранов, но остаётся в базе.</summary>
    [Fact]
    public async Task Мягко_удалённое_не_видно_без_снятия_фильтра()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Account account = await GivenAccountAsync(database);

        await using (FinanceDbContext context = await database.Contexts.CreateDbContextAsync())
        {
            Transaction expense = GivenExpense(account, 10m);
            expense.Delete(NowUtc);
            context.Transactions.Add(expense.ToRow());
            await context.SaveChangesAsync();
        }

        await using FinanceDbContext reader = await database.Contexts.CreateDbContextAsync();

        Assert.Empty(await reader.Transactions.ToListAsync());
        Assert.Single(await reader.Transactions.IgnoreQueryFilters().ToListAsync());
    }

    /// <summary>
    /// Метку изменения проставляет сохранение, а не обработчик: забытая в одном
    /// обработчике метка обнаружилась бы только при обмене.
    /// </summary>
    [Fact]
    public async Task Метка_изменения_проставляется_при_сохранении()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Account account = await GivenAccountAsync(database);

        DateTimeOffset stamped = await ScalarUpdatedAtAsync(database);

        Assert.True(stamped > account.UpdatedAtUtc, $"{stamped:O} не позже {account.UpdatedAtUtc:O}");
    }

    private static async Task<Account> GivenAccountAsync(TestDatabase database)
    {
        Account account = Account.Create(
            "Карта основная", AccountType.Card, Currency.RUB, 0m, new DateOnly(2026, 1, 1),
            excludedFromTotals: false, sortOrder: 0, Today, NowUtc);

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();
        context.Accounts.Add(account.ToRow());
        await context.SaveChangesAsync();

        return account;
    }

    private static Transaction GivenExpense(Account account, decimal amount) =>
        Transaction.Create(
            TransactionKind.Expense,
            account.Key,
            Money.Create(amount, account.Currency),
            targetAccountKey: null,
            targetAmount: null,
            categoryKey: Guid.CreateVersion7(),
            placeKey: null,
            occurredOn: Today,
            note: null,
            Today,
            NowUtc);

    private static async Task<DateTimeOffset> ScalarUpdatedAtAsync(TestDatabase database)
    {
        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        return await context.Accounts.Select(row => row.UpdatedAtUtc).SingleAsync();
    }

    /// <summary>Тип колонки по описанию таблицы — то, как SQLite её на самом деле завёл.</summary>
    private static async Task<string> ColumnTypeAsync(TestDatabase database, string table, string column)
    {
        object? value = await ExecuteScalarAsync(
            database,
            $"SELECT type FROM pragma_table_info('{table}') WHERE name = '{column}'");

        return (string)value!;
    }

    private static async Task<T> ScalarAsync<T>(TestDatabase database, string sql)
    {
        object? value = await ExecuteScalarAsync(database, sql);

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    private static async Task<object?> ExecuteScalarAsync(TestDatabase database, string sql)
    {
        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync();
    }
}
