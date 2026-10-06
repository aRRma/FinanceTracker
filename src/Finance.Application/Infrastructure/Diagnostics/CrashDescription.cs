using System.Collections.Frozen;
using System.Text;
using Finance.Application.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Текст сбоя для отчёта: тип и стек каждого исключения цепочки, а текст — только у тех типов, чей текст не несёт введённых значений.
/// </summary>
/// <remarks>
/// <see cref="FormatException"/> повторяет набранную строку, <see cref="ArgumentOutOfRangeException"/> — значение
/// аргумента, а сообщения о пропавшей записи называют группу по имени. Отчёт уходит из телефона руками,
/// поэтому список разрешённых короткий и пополняется только типами с текстом без данных пользователя.
/// </remarks>
internal static class CrashDescription
{
    /// <summary>
    /// Глубже вложенные исключения не разворачиваются: цепочка из сотни обёрток ничего не добавит к первым.
    /// </summary>
    private const int MaxDepth = 8;

    /// <summary>
    /// Типы, текст которых пишется: сорванная миграция говорит словами из ресурсов, SQLite называет
    /// таблицу и колонку, но не значение, остальные три — тип и имя объекта.
    /// </summary>
    private static readonly FrozenSet<Type> Explained = FrozenSet.Create(
        typeof(DatabaseMigrationException),
        typeof(SqliteException),
        typeof(NullReferenceException),
        typeof(InvalidCastException),
        typeof(ObjectDisposedException));

    /// <summary>
    /// Начала строк, которыми Java открывает вложенное исключение.
    /// </summary>
    private static readonly string[] NestedPrefixes = ["Caused by: ", "Suppressed: "];

    /// <summary>
    /// Описывает сбой со всеми вложенными исключениями.
    /// </summary>
    /// <param name="error">Сбой.</param>
    internal static string Describe(Exception error)
    {
        StringBuilder text = new();
        Append(text, error, depth: 0);

        return text.ToString();
    }

    /// <summary>
    /// Описывает стек Java без сообщений: строки исключений сводятся к типу, вызовы остаются.
    /// </summary>
    /// <param name="stack">Стек в виде, какой печатает Java: тип с сообщением, вызовы, «Caused by».</param>
    /// <remarks>
    /// Сообщение Java тоже бывает чужим текстом: исключение .NET, ушедшее в Java, приходит туда обёрткой
    /// с собственным сообщением внутри. Строки продолжения многострочного сообщения отбрасываются целиком.
    /// </remarks>
    internal static string DescribeJava(string stack)
    {
        StringBuilder text = new();
        bool first = true;

        foreach (string raw in stack.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            string trimmed = line.TrimStart();

            if (first)
            {
                text.Append(TypeOf(trimmed)).Append('\n');
                first = false;
            }
            else if (trimmed.StartsWith("at ", StringComparison.Ordinal) || trimmed.StartsWith("... ", StringComparison.Ordinal))
            {
                text.Append(line).Append('\n');
            }
            else if (NestedPrefix(trimmed) is { } prefix)
            {
                text.Append(prefix).Append(TypeOf(trimmed[prefix.Length..])).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Начало строки, которым Java открывает вложенное исключение; пусто — строка не такая.
    /// </summary>
    private static string? NestedPrefix(string line)
    {
        foreach (string prefix in NestedPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return prefix;
            }
        }

        return null;
    }

    /// <summary>
    /// Тип из строки исключения Java: всё до первого двоеточия, за которым идёт сообщение.
    /// </summary>
    private static string TypeOf(string line) => line.Split(':', 2)[0].Trim();

    private static void Append(StringBuilder text, Exception error, int depth)
    {
        text.Append(error.GetType().FullName);

        if (Explained.Contains(error.GetType()))
        {
            text.Append(": ").Append(error.Message);
        }

        text.Append('\n');

        if (error.StackTrace is { } stack)
        {
            text.Append(stack).Append('\n');
        }

        if (depth >= MaxDepth)
        {
            return;
        }

        // У составного исключения вложенных несколько, и первое из них — его же InnerException
        IEnumerable<Exception> inner = error switch
        {
            AggregateException aggregate => aggregate.InnerExceptions,
            { InnerException: { } single } => [single],
            _ => [],
        };

        foreach (Exception next in inner)
        {
            text.Append("--- inner ").Append(depth + 1).Append(": ");
            Append(text, next, depth + 1);
        }
    }
}
