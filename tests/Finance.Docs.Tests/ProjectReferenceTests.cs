namespace Finance.Docs.Tests;

/// <summary>
/// Приложение не ссылается на <c>Finance.Import</c>. Перенос получает доступ к строкам
/// таблиц через <c>InternalsVisibleTo</c> и пишет мимо обработчиков форм, а в пакет
/// для телефона он попасть не должен: ссылку из <c>Finance.App</c> не заметили бы
/// ни сборка, ни <c>dotnet test</c> — тот <c>Finance.App</c> не собирает.
/// </summary>
public sealed class ProjectReferenceTests
{
    private const string ImportProject = "Finance.Import";

    [Fact]
    public void Приложение_не_ссылается_на_перенос()
    {
        string project = File.ReadAllText(Path.Combine(Repository.Root, "src", "Finance.App", "Finance.App.csproj"));

        Assert.DoesNotContain(
            ImportProject,
            project,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Файл приложения вообще прочитан и ссылается на прикладной слой. Ошибись путь —
    /// проверка выше прошла бы на чужом или пустом тексте и перестала бы что-либо значить.
    /// </summary>
    [Fact]
    public void Файл_проекта_приложения_найден()
    {
        string project = File.ReadAllText(Path.Combine(Repository.Root, "src", "Finance.App", "Finance.App.csproj"));

        Assert.Contains("Finance.Application", project, StringComparison.Ordinal);
    }
}
