#!/usr/bin/env dotnet
#:property PublishAot=false
#:property RunWorkingDirectory=$(MSBuildStartupDirectory)

// Пересборка значков макетов: блок <defs> в docs/ui/mockups.html получает по символу на каждый
// ключ, на который ссылается разметка (href="#i-ключ"), с контуром из data/icon-paths.json —
// тем же, что рисует приложение. Рисованные символы h-… (вкладки, «назад», поиск) остаются,
// пока на них есть ссылка: в наборе приложения их нет.
// dotnet tools/mockup_icons.cs            запускать из корня репозитория; следом — сборщик прототипа

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

string file = Path.GetFullPath("docs/ui/mockups.html");
string paths = Path.GetFullPath("data/icon-paths.json");

if (!File.Exists(file) || !File.Exists(paths))
{
    Console.Error.WriteLine("запускать из корня репозитория");
    return 1;
}

string html = File.ReadAllText(file, Encoding.UTF8);
using JsonDocument json = JsonDocument.Parse(File.ReadAllText(paths, Encoding.UTF8));
JsonElement icons = json.RootElement.GetProperty("icons");

SortedSet<string> used = new(StringComparer.Ordinal);
foreach (Match reference in Regex.Matches(html, "href=\"#i-([a-z0-9-]+)\""))
{
    used.Add(reference.Groups[1].Value);
}

string[] missing = [.. used.Where(key => !icons.TryGetProperty(key, out _))];
if (missing.Length > 0)
{
    Console.Error.WriteLine($"нет контура в наборе: {string.Join(", ", missing)}");
    return 1;
}

// Переводы строк — как в самом файле: на Windows его отдаёт git с CRLF
string newline = html.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
StringBuilder defs = new();

foreach (Match drawn in Regex.Matches(html, "<symbol id=\"h-([a-z0-9-]+)\"[^\\n]*</symbol>"))
{
    if (html.Contains($"href=\"#h-{drawn.Groups[1].Value}\"", StringComparison.Ordinal))
    {
        defs.Append(drawn.Value.TrimEnd('\r')).Append(newline);
    }
}

foreach (string key in used)
{
    defs.Append($"<symbol id=\"i-{key}\" viewBox=\"0 0 24 24\"><path d=\"{icons.GetProperty(key).GetString()}\"/></symbol>{newline}");
}

int start = html.IndexOf("<defs>", StringComparison.Ordinal) + "<defs>".Length;
int end = html.IndexOf("</defs>", StringComparison.Ordinal);
File.WriteAllText(file, html[..start] + newline + defs + html[end..], new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

Console.WriteLine($"значков из набора: {used.Count}");
return 0;
