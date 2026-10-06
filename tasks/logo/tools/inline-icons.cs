#:property PublishAot=false
// Встраивает значки страницы концепций как data: URI — просмотрщик артефактов не отдаёт соседние файлы картинкам.
using System.Text;

string root = args[0];
string output = args[1];
Dictionary<string, string> icons = new()
{
    ["icons/before.svg"] = "concepts/n1-glass-emerald.svg",
    ["icons/more.svg"] = "concepts/n1-glass-emerald-v2.svg",
    ["icons/most.svg"] = "concepts/n1-glass-emerald-v3.svg",
    ["icons/neon2.svg"] = "concepts/n5-glass-neon-v2.svg",
    ["icons/neon.svg"] = "concepts/n5-glass-neon.svg",
    ["icons/now.svg"] = "concepts/now.svg",
};

string html = File.ReadAllText(Path.Combine(root, "logo-concepts.html"), Encoding.UTF8);
foreach ((string published, string source) in icons)
{
    byte[] svg = File.ReadAllBytes(Path.Combine(root, source));
    string uri = "data:image/svg+xml;base64," + Convert.ToBase64String(svg);
    int count = html.Split(published).Length - 1;
    html = html.Replace(published, uri, StringComparison.Ordinal);
    Console.WriteLine($"{published}: {count}");
}

File.WriteAllText(output, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"-> {output}");
