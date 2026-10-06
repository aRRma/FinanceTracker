#!/usr/bin/env dotnet
#:property PublishAot=false

// Покрытие кода тестами: прогон с замером, сведение отчётов, итог текстом.
// dotnet tools/coverage.cs                          итог по сборкам, папкам и файлам с пробелами
// dotnet tools/coverage.cs -- ИмяФайла [ещё...]     непокрытые строки и ветвления файлов (без .cs)
// dotnet tools/coverage.cs -- --changed [ветка]     то же, но только строки src, изменённые в ветке и рабочем
//                                                   дереве от общего предка с веткой (по умолчанию master),
//                                                   вместе с новыми файлами
//
// Общий процент от одной правки почти не сдвигается: 30 непокрытых строк новой функции
// тонут в шести тысячах. Режим изменённых строк показывает ровно их.
//
// Отчёт Cobertura пишется на каждый тестовый проект отдельно и видит только свои
// попадания: строка домена, которую покрывают лишь прикладные тесты, в отчёте
// доменных тестов числится непокрытой. Поэтому строки сводятся объединением
// по (сборка, файл, номер), а не берутся из одного отчёта. Миграции не считаются.

using System.Diagnostics;
using System.Xml.Linq;

string results = Path.Combine(Path.GetTempPath(), "finance-coverage");

// Изменённые строки читаются до прогона: ошибка git видна сразу, а не через минуту тестов
Dictionary<string, HashSet<int>>? changed = null;

if (args is ["--changed", ..])
{
    changed = await ChangedLinesAsync(args.Length > 1 ? args[1] : "master");

    if (changed is null)
    {
        return 1;
    }
}

if (Directory.Exists(results))
{
    Directory.Delete(results, recursive: true);
}

if (!await RunTestsAsync(results))
{
    return 1;
}

Dictionary<LineKey, LineHit> lines = Merge(Directory.GetFiles(results, "coverage.cobertura.xml", SearchOption.AllDirectories));
List<KeyValuePair<LineKey, LineHit>> counted = [.. lines.Where(static line => IsWritten(line.Key.File))];

if (changed is not null)
{
    PrintChanged(counted, changed);

    return 0;
}

if (args.Length > 0)
{
    foreach (string name in args)
    {
        PrintFile(counted, name);
    }

    return 0;
}

Console.WriteLine("Сборки (строки, ветвления):");

foreach (IGrouping<string, KeyValuePair<LineKey, LineHit>> assembly in counted.GroupBy(static line => line.Key.Assembly).OrderBy(static group => group.Key))
{
    Console.WriteLine($"  {assembly.Key,-22} {Share(assembly)}   {BranchShare(assembly)}");
}

Console.WriteLine();
Console.WriteLine("Папки:");

foreach (IGrouping<string, KeyValuePair<LineKey, LineHit>> folder in counted.GroupBy(static line => Folder(line.Key.File)).OrderBy(static group => group.Key))
{
    Console.WriteLine($"  {folder.Key,-50} {Share(folder)}");
}

Console.WriteLine();
Console.WriteLine("Больше всего непокрытых строк:");

foreach (IGrouping<string, KeyValuePair<LineKey, LineHit>> file in counted
             .GroupBy(static line => line.Key.File)
             .Where(static group => group.Any(static line => !line.Value.Hit))
             .OrderByDescending(static group => group.Count(static line => !line.Value.Hit))
             .Take(15))
{
    Console.WriteLine($"  {file.Count(static line => !line.Value.Hit),4} из {file.Count(),-4} {file.Key}");
}

return 0;

// Прогон всего набора с замером. Вывод теста показывается только при падении:
// покрытие упавшего набора ничего не значит
static async Task<bool> RunTestsAsync(string results)
{
    ProcessStartInfo start = new("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    foreach (string argument in (string[])["test", "-v", "q", "--collect:XPlat Code Coverage", "--results-directory", results])
    {
        start.ArgumentList.Add(argument);
    }

    using Process process = Process.Start(start)!;
    Task<string> error = process.StandardError.ReadToEndAsync();
    string output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode is not 0)
    {
        Console.Error.WriteLine(output);
        Console.Error.WriteLine(await error);
        Console.Error.WriteLine("тесты не прошли — покрытие не считается");

        return false;
    }

    return true;
}

static Dictionary<LineKey, LineHit> Merge(string[] reports)
{
    Dictionary<LineKey, LineHit> lines = [];

    foreach (string report in reports)
    {
        XDocument document = XDocument.Load(report);

        // Имя файла в отчёте — от его корня <source>, а корень у каждого отчёта свой:
        // у доменных тестов это папка домена, у прикладных — src. Без корня одна строка
        // домена числилась бы дважды, под двумя путями, и отчёты не сводились бы
        string root = document.Descendants("source").Single().Value;

        foreach (XElement package in document.Descendants("package"))
        foreach (XElement type in package.Descendants("class"))
        foreach (XElement line in type.Element("lines")!.Elements("line"))
        {
            LineKey key = new(
                package.Attribute("name")!.Value,
                Relative(Path.Combine(root, type.Attribute("filename")!.Value)),
                int.Parse(line.Attribute("number")!.Value));

            LineHit hit = new(int.Parse(line.Attribute("hits")!.Value) > 0, Branches(line));

            lines[key] = lines.TryGetValue(key, out LineHit known) ? known.With(hit) : hit;
        }
    }

    return lines;
}

// «50% (1/2)» — покрыто ветвлений из всех
static (int Covered, int Total) Branches(XElement line)
{
    string? condition = line.Attribute("condition-coverage")?.Value;

    if (condition is null)
    {
        return (0, 0);
    }

    string[] parts = condition[(condition.IndexOf('(') + 1)..^1].Split('/');

    return (int.Parse(parts[0]), int.Parse(parts[1]));
}

// Путь в отчёте — от папки src: «Finance.Application/Features/…»
static string Relative(string path)
{
    string normalized = path.Replace('\\', '/');
    int source = normalized.IndexOf("/src/", StringComparison.Ordinal);

    return source < 0 ? normalized : normalized[(source + "/src/".Length)..];
}

// Написанное руками: сгенерированное в obj (ресурсы, разбор JSON) и миграции
// тестами не пишутся, а в счёте утопили бы пробелы настоящего кода
static bool IsWritten(string file) =>
    !file.Contains("/obj/", StringComparison.Ordinal) && !file.Contains("/Migrations/", StringComparison.Ordinal);

// Слайс делится до экрана: папка Features/Accounts целиком не говорит,
// карточка осталась без тестов или справочник
static string Folder(string file)
{
    string[] parts = file.Split('/');
    int depth = parts.Length > 2 && parts[1] is "Features" ? 4 : 3;

    return string.Join('/', parts.Take(Math.Min(parts.Length - 1, depth)));
}

static string Share(IEnumerable<KeyValuePair<LineKey, LineHit>> lines)
{
    int total = lines.Count();
    int hit = lines.Count(static line => line.Value.Hit);

    return $"{hit,5}/{total,-5} {100.0 * hit / total,5:F1}%";
}

static string BranchShare(IEnumerable<KeyValuePair<LineKey, LineHit>> lines)
{
    int covered = lines.Sum(static line => line.Value.Branches.Covered);
    int total = lines.Sum(static line => line.Value.Branches.Total);

    return total is 0 ? "ветвлений нет" : $"ветвления {covered}/{total} {100.0 * covered / total:F1}%";
}

static void PrintFile(List<KeyValuePair<LineKey, LineHit>> lines, string name)
{
    IEnumerable<IGrouping<string, KeyValuePair<LineKey, LineHit>>> files = lines
        .Where(line => line.Key.File.EndsWith($"/{name}.cs", StringComparison.Ordinal))
        .GroupBy(static line => line.Key.File);

    bool found = false;

    foreach (IGrouping<string, KeyValuePair<LineKey, LineHit>> file in files)
    {
        found = true;

        IEnumerable<int> missed = file.Where(static line => !line.Value.Hit).Select(static line => line.Key.Number).Order();
        IEnumerable<string> partial = file
            .Where(static line => line.Value.Hit && line.Value.Branches.Covered < line.Value.Branches.Total)
            .OrderBy(static line => line.Key.Number)
            .Select(static line => $"{line.Key.Number}({line.Value.Branches.Covered}/{line.Value.Branches.Total})");

        Console.WriteLine(file.Key);
        Console.WriteLine($"  строки {Share(file)}");
        Console.WriteLine($"  не покрыты: {string.Join(' ', missed)}");
        Console.WriteLine($"  ветвления частично: {string.Join(' ', partial)}");
    }

    if (!found)
    {
        Console.WriteLine($"{name}.cs: в отчётах нет — файл не из src или имя с опечаткой");
    }
}

// Строки src, изменённые относительно ветки, по файлам; путь — от папки src, как в отчёте.
// Сравнивается рабочее дерево, а не коммиты: правка проверяется до коммита. Новые
// файлы git diff не показывает вовсе — они берутся целиком из списка неотслеживаемых
static async Task<Dictionary<string, HashSet<int>>?> ChangedLinesAsync(string baseBranch)
{
    // От общего предка, а не от вершины ветки: ушедший вперёд master показался бы
    // правкой этой ветки — его новые строки в дереве «изменены» относительно него
    (int baseExit, string mergeBase) = await GitAsync("merge-base", baseBranch, "HEAD");
    (int diffExit, string diff) = await GitAsync("diff", "-U0", "--no-color", mergeBase.Trim(), "--", "src");
    (int listExit, string untracked) = await GitAsync("ls-files", "--others", "--exclude-standard", "--", "src");

    if (baseExit is not 0 || diffExit is not 0 || listExit is not 0)
    {
        Console.Error.WriteLine($"git не отдал изменения относительно {baseBranch}");

        return null;
    }

    Dictionary<string, HashSet<int>> changed = [];
    HashSet<int>? current = null;

    foreach (string line in diff.ReplaceLineEndings("\n").Split('\n'))
    {
        if (line.StartsWith("+++ ", StringComparison.Ordinal))
        {
            // «+++ /dev/null» — файл удалён, строк в нём больше нет
            current = line is "+++ /dev/null" ? null : Changed(changed, Relative(line[4..]));
        }
        else if (line.StartsWith("@@ ", StringComparison.Ordinal) && current is not null)
        {
            // «@@ -12,3 +14,5 @@»: новые строки — с 14-й, пять штук; без числа — одна
            string[] added = line.Split(' ')[2][1..].Split(',');
            int start = int.Parse(added[0]);
            int count = added.Length > 1 ? int.Parse(added[1]) : 1;

            for (int number = start; number < start + count; number++)
            {
                current.Add(number);
            }
        }
    }

    foreach (string file in untracked.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        if (file.EndsWith(".cs", StringComparison.Ordinal))
        {
            // Путь от корня репозитория, «src/…»: косая спереди — чтобы Relative нашёл /src/
            HashSet<int> lines = Changed(changed, Relative($"/{file}"));

            // Число строк — один раз: условие цикла перечитывало бы файл на каждой строке
            int total = File.ReadLines(file).Count();

            for (int number = 1; number <= total; number++)
            {
                lines.Add(number);
            }
        }
    }

    return changed;

    static HashSet<int> Changed(Dictionary<string, HashSet<int>> changed, string file)
    {
        if (!changed.TryGetValue(file, out HashSet<int>? lines))
        {
            lines = [];
            changed[file] = lines;
        }

        return lines;
    }
}

static async Task<(int ExitCode, string Output)> GitAsync(params string[] arguments)
{
    ProcessStartInfo start = new("git")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    foreach (string argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using Process process = Process.Start(start)!;
    Task<string> error = process.StandardError.ReadToEndAsync();
    string output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode is not 0)
    {
        Console.Error.WriteLine(await error);
    }

    return (process.ExitCode, output);
}

// Только изменённые строки, которые отчёт считает исполнимыми: комментарии и пустые
// строки в отчёт не попадают и пробелом не считаются
static void PrintChanged(List<KeyValuePair<LineKey, LineHit>> lines, Dictionary<string, HashSet<int>> changed)
{
    List<KeyValuePair<LineKey, LineHit>> touched =
        [.. lines.Where(line => changed.TryGetValue(line.Key.File, out HashSet<int>? numbers) && numbers.Contains(line.Key.Number))];

    if (touched.Count is 0)
    {
        Console.WriteLine("Изменённых исполнимых строк в src нет.");

        return;
    }

    Console.WriteLine($"Изменённые строки: {Share(touched)}   {BranchShare(touched)}");

    foreach (IGrouping<string, KeyValuePair<LineKey, LineHit>> file in touched
                 .GroupBy(static line => line.Key.File)
                 .OrderBy(static group => group.Key))
    {
        int[] missed = [.. file.Where(static line => !line.Value.Hit).Select(static line => line.Key.Number).Order()];
        string[] partial =
        [
            .. file
                .Where(static line => line.Value.Hit && line.Value.Branches.Covered < line.Value.Branches.Total)
                .OrderBy(static line => line.Key.Number)
                .Select(static line => $"{line.Key.Number}({line.Value.Branches.Covered}/{line.Value.Branches.Total})")
        ];

        if (missed.Length is 0 && partial.Length is 0)
        {
            continue;
        }

        Console.WriteLine();
        Console.WriteLine(file.Key);
        Console.WriteLine($"  строки {Share(file)}");

        if (missed.Length > 0)
        {
            Console.WriteLine($"  не покрыты: {string.Join(' ', missed)}");
        }

        if (partial.Length > 0)
        {
            Console.WriteLine($"  ветвления частично: {string.Join(' ', partial)}");
        }
    }
}

/// <summary>
/// Строка исходника в отчёте: сборка, путь от корня репозитория и номер.
/// </summary>
readonly record struct LineKey(string Assembly, string File, int Number);

/// <summary>
/// Что известно о строке: исполнялась ли и сколько её ветвлений пройдено.
/// </summary>
readonly record struct LineHit(bool Hit, (int Covered, int Total) Branches)
{
    /// <summary>
    /// Сводит два отчёта об одной строке: исполнена хотя бы в одном, ветвлений
    /// пройдено столько, сколько в лучшем.
    /// </summary>
    public LineHit With(LineHit other) => new(
        Hit || other.Hit,
        (Math.Max(Branches.Covered, other.Branches.Covered), Math.Max(Branches.Total, other.Branches.Total)));
}
