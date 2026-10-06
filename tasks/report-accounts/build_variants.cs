// Собирает variants.html: голова и значки из docs/ui/mockups.html, тело — variants.body.html.
// Запуск из корня репозитория: dotnet tasks/report-accounts/build_variants.cs
using System.Text;

string mockups = File.ReadAllText("docs/ui/mockups.html", Encoding.UTF8);
string body = File.ReadAllText("tasks/report-accounts/variants.body.html", Encoding.UTF8);

// Всё до обёртки страницы: стили, тема, спрайт значков
int wrap = mockups.IndexOf("<div class=\"wrap\">", StringComparison.Ordinal);
if (wrap < 0)
{
    throw new InvalidOperationException("В mockups.html не найдена обёртка страницы.");
}

string head = mockups[..wrap].Replace(
    "<title>Личный трекер финансов — макеты, итерация 5</title>",
    "<title>Выбор счетов в отчёте — макет</title>",
    StringComparison.Ordinal);

File.WriteAllText(
    "tasks/report-accounts/variants.html",
    head + body + "\n</body>\n</html>\n",
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine("variants.html собран");
