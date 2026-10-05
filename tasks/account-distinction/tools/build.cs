#!/usr/bin/env dotnet
#:property PublishAot=false
#:property RunWorkingDirectory=$(MSBuildStartupDirectory)

// Сборка набросков задачи. Запускать из корня репозитория после правки макетов,
// набора значков или документов задачи.
// dotnet tasks/account-distinction/tools/build.cs
//
// 1. kit/base.css — <style> из docs/ui/mockups.html: документы стоят на тех же стилях,
//    что макеты приложения, и не расходятся с ними. Пути шрифтов пересчитаны от kit/.
// 2. kit/icons.js — все контуры data/icon-paths.json, рисованные h-… из макетов и фильтры
//    симуляции дальтонизма: экраны собираются в kit.js, и ссылки на значки там вычисляются.
// 3. variants.html (обзор) — блоки между метками shared-style и icons, как раньше.
// 4. artifacts/dist/*.html — каждый документ одним файлом: стили, скрипты и шрифты внутри,
//    чтобы открыть на телефоне без репозитория. Папка artifacts в git не попадает.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

string task = Path.GetFullPath("tasks/account-distinction");
string mockups = Path.GetFullPath("docs/ui/mockups.html");
string paths = Path.GetFullPath("data/icon-paths.json");
string fonts = Path.GetFullPath("src/Finance.App/Resources/Fonts");

if (!Directory.Exists(task) || !File.Exists(mockups) || !File.Exists(paths))
{
    Console.Error.WriteLine("запускать из корня репозитория");
    return 1;
}

UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false);
string source = File.ReadAllText(mockups, Encoding.UTF8);
using JsonDocument json = JsonDocument.Parse(File.ReadAllText(paths, Encoding.UTF8));

string style = Regex.Match(source, "<style>(.*?)</style>", RegexOptions.Singleline).Groups[1].Value;
File.WriteAllText(Path.Combine(task, "kit/base.css"),
    "/* Создан tools/build.cs из docs/ui/mockups.html — руками не править */\n" + style.Replace("url(../../src/", "url(../../../src/", StringComparison.Ordinal), utf8);

Dictionary<string, string> drawn = Regex.Matches(source, "<symbol id=\"(h-[a-z-]+)\".*?</symbol>")
    .ToDictionary(static m => m.Groups[1].Value, static m => m.Value);
StringBuilder sprite = new("<svg width=\"0\" height=\"0\" style=\"position:absolute\" aria-hidden=\"true\"><defs>");
foreach (string symbol in drawn.Values)
{
    sprite.Append(symbol);
}

foreach (JsonProperty icon in json.RootElement.GetProperty("icons").EnumerateObject())
{
    sprite.Append($"<symbol id=\"i-{icon.Name}\" viewBox=\"0 0 24 24\"><path d=\"{icon.Value.GetString()}\"/></symbol>");
}

// Симуляция дальтонизма: Machado, Oliveira, Fernandes 2009, полная степень
sprite.Append("<filter id=\"deutan\" color-interpolation-filters=\"linearRGB\"><feColorMatrix type=\"matrix\" values=\"0.367322 0.860646 -0.227968 0 0  0.280085 0.672501 0.047413 0 0  -0.011820 0.042940 0.968881 0 0  0 0 0 1 0\"/></filter>");
sprite.Append("<filter id=\"protan\" color-interpolation-filters=\"linearRGB\"><feColorMatrix type=\"matrix\" values=\"0.152286 1.052583 -0.204868 0 0  0.114503 0.786281 0.099216 0 0  -0.003882 -0.048116 1.051998 0 0  0 0 0 1 0\"/></filter>");
sprite.Append("</defs></svg>");
File.WriteAllText(Path.Combine(task, "kit/icons.js"),
    "// Создан tools/build.cs из data/icon-paths.json и docs/ui/mockups.html — руками не править\n" +
    $"document.body.insertAdjacentHTML(\"afterbegin\", {JsonSerializer.Serialize(sprite.ToString())});\n", utf8);

string overview = Path.Combine(task, "variants.html");
if (File.Exists(overview))
{
    string html = File.ReadAllText(overview, Encoding.UTF8);
    html = Replace(html, "shared-style", $"<style>{style}</style>");
    html = Replace(html, "icons", Regex.Replace(sprite.ToString(), "<filter.*?</filter>", ""));
    File.WriteAllText(overview, html, utf8);
}

string dist = Path.Combine(task, "artifacts/dist");
Directory.CreateDirectory(dist);
string Font(string name) => "data:font/ttf;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(fonts, name)));
string regular = Font("OpenSans-Regular.ttf"), semibold = Font("OpenSans-Semibold.ttf");

foreach (string page in Directory.GetFiles(task, "*.html").Where(static p => !p.EndsWith("variants.html", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
{
    string html = File.ReadAllText(page, Encoding.UTF8);
    html = Regex.Replace(html, "<link rel=\"stylesheet\" href=\"(kit/[^\"]+)\">", m =>
    {
        string css = File.ReadAllText(Path.Combine(task, m.Groups[1].Value), Encoding.UTF8)
            .Replace("url(../../../src/Finance.App/Resources/Fonts/OpenSans-Regular.ttf)", $"url({regular})", StringComparison.Ordinal)
            .Replace("url(../../../src/Finance.App/Resources/Fonts/OpenSans-Semibold.ttf)", $"url({semibold})", StringComparison.Ordinal);
        return $"<style>{css}</style>";
    });
    html = Regex.Replace(html, "<script src=\"(kit/[^\"]+)\"></script>", m =>
        $"<script>{File.ReadAllText(Path.Combine(task, m.Groups[1].Value), Encoding.UTF8)}</script>");
    File.WriteAllText(Path.Combine(dist, Path.GetFileName(page)), html, utf8);
    Console.WriteLine($"{Path.GetFileName(page)}: {new FileInfo(Path.Combine(dist, Path.GetFileName(page))).Length / 1024} КБ");
}

return 0;

static string Replace(string html, string mark, string content)
{
    string open = $"<!--{mark}-->", close = $"<!--/{mark}-->";
    int start = html.IndexOf(open, StringComparison.Ordinal), end = html.IndexOf(close, StringComparison.Ordinal);
    if (start < 0 || end < start)
    {
        throw new InvalidOperationException($"нет меток {open}…{close}");
    }

    return $"{html[..(start + open.Length)]}\n{content}\n{html[end..]}";
}
