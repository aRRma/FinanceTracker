using System.Globalization;
using System.Text;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Трасса зависания — текст, который Android прикладывает к записи о нём. Из неё берутся тема и стек
/// главного потока: на нём и встало приложение.
/// </summary>
/// <remarks>
/// Главный поток в трассе бывает в двух видах: стеком Java (<c>"main" prio=5 tid=1</c>) и нативным
/// (<c>"имя процесса" sysTid=pid</c>) — на эмуляторе пришёл только второй. Берутся оба, что нашлись.
/// </remarks>
internal static class AnrTrace
{
    /// <summary>
    /// Предел описания в символах: трасса целиком — десятки килобайт, а причина видна в начале стека.
    /// </summary>
    internal const int Limit = 16 * 1024;

    private const string Subject = "Subject: ";
    private const string ProcessHeader = "----- pid ";

    /// <summary>
    /// Описывает зависание: тема от системы и стек главного потока.
    /// </summary>
    /// <param name="trace">Трасса целиком.</param>
    internal static string Describe(string trace)
    {
        string[] lines = trace.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        StringBuilder text = new();

        if (Array.Find(lines, static line => line.StartsWith(Subject, StringComparison.Ordinal)) is { } subject)
        {
            text.Append(subject).Append('\n');
        }

        // Только секция первого процесса — этого приложения: в трассе бывают дампы и других
        // процессов, и у каждого свой "main", чужой стек заслонил бы свой и съел бы предел
        int start = Array.FindIndex(lines, static line => line.StartsWith(ProcessHeader, StringComparison.Ordinal));
        string? pid = start < 0 ? null : lines[start][ProcessHeader.Length..].Split(' ')[0];
        int end = pid is null ? lines.Length : SectionEnd(lines, start, pid);

        for (int index = Math.Max(start, 0); index < end; index++)
        {
            if (IsMainThread(lines[index], pid))
            {
                index = AppendBlock(lines, index, end, text);
            }
        }

        return text.Length <= Limit ? text.ToString() : text.ToString(0, Limit) + "\n...\n";
    }

    /// <summary>
    /// Конец секции процесса: строка <c>----- end pid -----</c> или начало секции следующего процесса.
    /// </summary>
    private static int SectionEnd(string[] lines, int start, string pid)
    {
        string end = string.Create(CultureInfo.InvariantCulture, $"----- end {pid} ");

        for (int index = start + 1; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(end, StringComparison.Ordinal) || lines[index].StartsWith(ProcessHeader, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return lines.Length;
    }

    private static bool IsMainThread(string line, string? pid) =>
        line.StartsWith("\"main\" ", StringComparison.Ordinal)
        || (pid is not null
            && line.StartsWith('"')
            && line.EndsWith(string.Create(CultureInfo.InvariantCulture, $" sysTid={pid}"), StringComparison.Ordinal));

    /// <summary>
    /// Дописывает блок потока — до пустой строки — и возвращает номер его последней строки.
    /// </summary>
    private static int AppendBlock(string[] lines, int start, int end, StringBuilder text)
    {
        int index = start;

        for (; index < end && lines[index].Length > 0; index++)
        {
            text.Append(lines[index]).Append('\n');
        }

        return index;
    }
}
