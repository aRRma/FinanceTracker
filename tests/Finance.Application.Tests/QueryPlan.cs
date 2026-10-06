using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// План запроса от самой SQLite. Общий для ленты и отчёта: по составу колонок
/// индекса не видно, обслужит ли он сортировку или фильтр, — это видно только плану.
/// </summary>
internal static partial class QueryPlan
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

    /// <summary>
    /// Псевдоним таблицы операций в SQL. EF выбирает его сам, поэтому он
    /// вынимается из запроса, а не угадывается.
    /// </summary>
    public static string TransactionsAlias(string sql) => TransactionsAliasPattern().Match(sql).Groups[1].Value;

    /// <summary>
    /// Сверяет, что запрос не идёт полным проходом по таблице операций. Для сводных
    /// запросов это единственная сверка плана: планировщик вправе пойти и от счетов,
    /// и от операций, оба плана хороши — плохо ровно одно, полный проход.
    /// </summary>
    public static async Task NoFullScanOfTransactionsAsync(TestDatabase database, string sql)
    {
        string plan = await ExplainAsync(database, sql);

        NoFullScan(plan, TransactionsAlias(sql));
    }

    /// <summary>
    /// Сверяет, что таблица под псевдонимом не идёт полным проходом. Проход бывает
    /// и по индексу — «SCAN t USING INDEX …»: индекс тогда задаёт лишь порядок обхода,
    /// а читается вся таблица. Сверка одного голого «SCAN t» его пропускала.
    /// </summary>
    private static void NoFullScan(string plan, string alias)
    {
        Assert.False(string.IsNullOrEmpty(alias), "Псевдоним таблицы не найден в SQL.");

        string scan = $"SCAN {alias}";

        foreach (string line in plan.ReplaceLineEndings("\n").Split('\n'))
        {
            Assert.False(line == scan || line.StartsWith($"{scan} ", StringComparison.Ordinal), plan);
        }
    }

    [GeneratedRegex("\"transactions\" AS \"(\\w+)\"")]
    private static partial Regex TransactionsAliasPattern();
}
