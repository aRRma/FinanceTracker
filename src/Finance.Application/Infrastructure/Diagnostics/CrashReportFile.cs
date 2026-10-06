using System.Globalization;
using System.Text;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Формат файла отчётов о сбоях: обычный текст, который читается глазами после «Поделиться». Каждый отчёт —
/// строка заголовка с моментом и видом, тело и строка конца.
/// </summary>
/// <remarks>
/// Отчёт без строки конца — оборванная запись: процесс умер посреди неё. Такой отчёт пропускается, а следующие
/// за ним читаются: строка заголовка открывает новый отчёт, даже если прежний не закрыт. Строка тела, похожая
/// на служебную, пишется с пробелом впереди и потому служебной не считается.
/// </remarks>
internal static class CrashReportFile
{
    private const string Marker = "===";
    private const string End = "=== end";

    /// <summary>
    /// Строка, после которой идёт след действий.
    /// </summary>
    internal const string TrailHeader = "--- trail";

    /// <summary>
    /// Собирает отчёт в текст для дозаписи в файл.
    /// </summary>
    /// <param name="atUtc">Момент сбоя.</param>
    /// <param name="kind">Вид.</param>
    /// <param name="device">Сборка и устройство.</param>
    /// <param name="description">Сбой.</param>
    /// <param name="trail">След действий перед сбоем, от старых к новым.</param>
    internal static string Format(DateTimeOffset atUtc, CrashKind kind, DeviceInfo device, string description, IReadOnlyList<string> trail)
    {
        StringBuilder text = new();
        text.Append(CultureInfo.InvariantCulture, $"{Marker} {atUtc.ToUniversalTime():O} {kind}\n");
        text.Append(CultureInfo.InvariantCulture, $"app {device.AppVersion} ({device.AppBuild}), android {device.Android}, {device.Model}\n");

        AppendLines(text, description);

        if (trail.Count > 0)
        {
            text.Append(TrailHeader).Append('\n');

            foreach (string entry in trail)
            {
                AppendLines(text, entry);
            }
        }

        return text.Append(End).Append('\n').ToString();
    }

    private static void AppendLines(StringBuilder text, string lines)
    {
        foreach (string line in lines.Split('\n'))
        {
            if (line.Length is 0)
            {
                continue;
            }

            text.Append(line.StartsWith(Marker, StringComparison.Ordinal) ? " " : "").Append(line.TrimEnd('\r')).Append('\n');
        }
    }

    /// <summary>
    /// Разбирает файл в отчёты в порядке записи.
    /// </summary>
    /// <param name="text">Содержимое файла.</param>
    internal static IEnumerable<CrashReport> Parse(string text)
    {
        (DateTimeOffset AtUtc, CrashKind Kind)? open = null;
        List<string> body = [];

        foreach (string line in text.Split('\n'))
        {
            if (line == End)
            {
                if (open is { } header && body.Count > 0)
                {
                    yield return new CrashReport
                    {
                        AtUtc = header.AtUtc,
                        Kind = header.Kind,
                        Headline = body.Count > 1 ? body[1] : "",
                        Text = string.Join('\n', body),
                    };
                }

                open = null;
                body.Clear();
            }
            else if (line.StartsWith(Marker, StringComparison.Ordinal))
            {
                // Новый заголовок при незакрытом отчёте: прежний оборван и пропадает
                open = TryHeader(line);
                body.Clear();
            }
            else if (open is not null)
            {
                body.Add(line);
            }
        }
    }

    private static (DateTimeOffset, CrashKind)? TryHeader(string line)
    {
        string[] parts = line.Split(' ');

        return parts.Length is 3
            && DateTimeOffset.TryParseExact(parts[1], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset atUtc)
            && Enum.TryParse(parts[2], ignoreCase: false, out CrashKind kind)
            && Enum.IsDefined(kind)
                ? (atUtc, kind)
                : null;
    }
}
