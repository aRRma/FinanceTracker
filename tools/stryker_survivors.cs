#:property PublishAot=false
// Выжившие и непокрытые мутанты из последнего отчёта Stryker: файл, строка, мутация.
// Запуск из каталога проверяемого проекта после dotnet stryker:
//   dotnet ../../tools/stryker_survivors.cs                  все выжившие списком
//   dotnet ../../tools/stryker_survivors.cs -- --by-file     сколько выжило в каждом файле
//   dotnet ../../tools/stryker_survivors.cs -- ИмяФайла      выжившие одного файла (без .cs)
using System.Text.Json;

string output = Path.Combine(Directory.GetCurrentDirectory(), "StrykerOutput");

if (!Directory.Exists(output))
{
    Console.Error.WriteLine($"Нет папки {output}: сначала dotnet stryker в этом каталоге.");
    return 1;
}

string? report = Directory.GetDirectories(output)
    .OrderDescending()
    .Select(static run => Path.Combine(run, "reports", "mutation-report.json"))
    .FirstOrDefault(File.Exists);

if (report is null)
{
    Console.Error.WriteLine($"В {output} нет отчёта json: прогон оборвался или не включён репортёр json.");
    return 1;
}

Console.WriteLine(report);

using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(report));
int count = 0;
bool byFile = args is ["--by-file"];
string? only = args is [{ } name] && !byFile ? $"{name}.cs" : null;
Dictionary<string, (int Survived, int NoCoverage)> perFile = [];

foreach (JsonProperty file in document.RootElement.GetProperty("files").EnumerateObject())
{
    if (only is not null && !Path.GetFileName(file.Name).Equals(only, StringComparison.Ordinal))
    {
        continue;
    }

    string[] lines = file.Value.GetProperty("source").GetString()!.ReplaceLineEndings("\n").Split('\n');

    foreach (JsonElement mutant in file.Value.GetProperty("mutants").EnumerateArray())
    {
        string status = mutant.GetProperty("status").GetString()!;

        if (status is not ("Survived" or "NoCoverage"))
        {
            continue;
        }

        count++;

        if (byFile)
        {
            (int survived, int noCoverage) = perFile.GetValueOrDefault(file.Name);
            perFile[file.Name] = status is "Survived" ? (survived + 1, noCoverage) : (survived, noCoverage + 1);

            continue;
        }

        int line = mutant.GetProperty("location").GetProperty("start").GetProperty("line").GetInt32();
        string replacement = mutant.TryGetProperty("replacement", out JsonElement value) ? value.GetString() ?? "" : "";

        Console.WriteLine();
        Console.WriteLine($"{status} {Path.GetFileName(file.Name)}:{line} {mutant.GetProperty("mutatorName").GetString()}");
        Console.WriteLine($"  было:  {lines[line - 1].Trim()}");
        Console.WriteLine($"  стало: {replacement.ReplaceLineEndings(" ").Trim()}");
    }
}

if (byFile)
{
    Console.WriteLine("выжило  без покрытия  файл");

    foreach ((string file, (int survived, int noCoverage)) in perFile.OrderByDescending(static pair => pair.Value.Survived + pair.Value.NoCoverage))
    {
        string relative = file.Replace('\\', '/');
        int source = relative.IndexOf("/src/", StringComparison.Ordinal);

        Console.WriteLine($"{survived,6}  {noCoverage,12}  {(source < 0 ? relative : relative[(source + 5)..])}");
    }
}

Console.WriteLine();
Console.WriteLine($"Всего: {count}");
return 0;
