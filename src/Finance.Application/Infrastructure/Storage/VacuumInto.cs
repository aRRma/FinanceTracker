using Microsoft.Data.Sqlite;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Целостная копия файла базы средствами СУБД. Обычное копирование файла взяло бы
/// базу вместе с недописанным журналом, и копия оказалась бы нерабочей.
/// </summary>
internal static class VacuumInto
{
    /// <summary>
    /// Пишет копию базы в файл, заменяя прежний. Копия пишется рядом во временный
    /// файл и подменяет прежнюю только готовой.
    /// </summary>
    /// <param name="connectionString">Строка подключения к базе, с которой снимается копия.</param>
    /// <param name="target">Полный путь к файлу копии.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static async Task WriteAsync(string connectionString, string target, CancellationToken cancellationToken)
    {
        // Удали мы прежний файл заранее, сорвавшееся копирование — кончилось
        // место — оставило бы вовсе без копии; а VACUUM INTO в существующий
        // файл не пишет
        string draft = target + ".tmp";

        File.Delete(draft);

        await using (SqliteConnection connection = new(connectionString))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $target";
            command.Parameters.AddWithValue("$target", draft);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(draft, target, overwrite: true);
    }
}
