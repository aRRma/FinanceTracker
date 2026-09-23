#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false
#:property RunWorkingDirectory=$(MSBuildStartupDirectory)

// Раздача папки по http для снимков Playwright: протокол file: в нём заблокирован.
// dotnet tools/serve.cs -- [папка] [порт]    по умолчанию docs/ui и 8777
// Останавливается Ctrl+C или завершением процесса.

using Microsoft.Extensions.FileProviders;

string folder = Path.GetFullPath(args.Length > 0 ? args[0] : "docs/ui");
string port = args.Length > 1 ? args[1] : "8777";

if (!Directory.Exists(folder))
{
    Console.Error.WriteLine($"нет папки {folder}");
    return 1;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
WebApplication app = builder.Build();

// Без кэша: макет правят и тут же снимают, и старая копия в браузере выдала бы прежний экран
app.Use(static async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseFileServer(new FileServerOptions { FileProvider = new PhysicalFileProvider(folder) });

string url = $"http://127.0.0.1:{port}";
Console.WriteLine($"{folder} -> {url}/");
await app.RunAsync(url);
return 0;
