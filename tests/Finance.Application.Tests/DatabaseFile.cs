using Microsoft.Data.Sqlite;

namespace Finance.Application.Tests;

/// <summary>
/// Чтение файла базы в обход EF — для сверки выгрузки и восстановления: что
/// лежит в файле, проверяется самим файлом, а не моделью приложения.
/// </summary>
internal static class DatabaseFile
{
    /// <summary>
    /// Строка подключения без пула: пул держал бы файл после теста,
    /// и временная папка не удалилась бы.
    /// </summary>
    public static string Unpooled(string file) =>
        new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString();

    /// <summary>
    /// Все строки всех таблиц текстом, с именем таблицы впереди и в порядке, не
    /// зависящем от физического расположения строк в файле.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DumpAsync(string connectionString)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync();

        List<string> tables = [];

        await using (SqliteCommand list = connection.CreateCommand())
        {
            list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

            await using SqliteDataReader reader = await list.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        List<string> rows = [];

        foreach (string table in tables)
        {
            await using SqliteCommand select = connection.CreateCommand();
            select.CommandText = $"SELECT * FROM \"{table}\"";

            await using SqliteDataReader reader = await select.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                object[] values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(table + "|" + string.Join("|", values));
            }
        }

        rows.Sort(StringComparer.Ordinal);

        return rows;
    }

    /// <summary>
    /// Первое значение первой строки запроса.
    /// </summary>
    public static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// Выполняет команды над файлом.
    /// </summary>
    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }
}
