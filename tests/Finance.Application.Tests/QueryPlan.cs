using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// План запроса от самой SQLite. Общий для ленты и отчёта: по составу колонок
/// индекса не видно, обслужит ли он сортировку или фильтр, — это видно только плану.
/// </summary>
internal static class QueryPlan
{
    /// <summary>
    /// Снимает план запроса. Параметры подставлять незачем: план от их значений
    /// не зависит, а строки <c>.param set</c> впереди SQL — команды оболочки,
    /// и база на них спотыкается.
    /// </summary>
    public static Task<string> ExplainAsync<T>(TestDatabase database, IQueryable<T> query) =>
        ExplainAsync(database, query.ToQueryString());

    /// <summary>
    /// Снимает план готового SQL, в том числе с ведущими строками <c>.param set</c>.
    /// </summary>
    public static async Task<string> ExplainAsync(TestDatabase database, string sqlWithParameters)
    {
        string sql = string.Join(
            '\n',
            sqlWithParameters.Split('\n').SkipWhile(line => line.StartsWith('.') || line.Trim().Length is 0));

        await using SqliteConnection connection = new(database.Location.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";

        StringBuilder plan = new();

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            plan.AppendLine(reader.GetString(reader.GetOrdinal("detail")));
        }

        return plan.ToString();
    }
}
