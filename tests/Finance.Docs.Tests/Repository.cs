namespace Finance.Docs.Tests;

/// <summary>
/// Корень репозитория и файлы, которые проверяют тесты этого проекта.
/// </summary>
internal static class Repository
{
    /// <summary>
    /// Каталог репозитория. Ищется вверх от каталога сборки по файлу решения:
    /// путь от bin к корню зависит от конфигурации и целевого фреймворка,
    /// поэтому считать уровни «..» нельзя — сломается при первом же изменении сборки.
    /// </summary>
    public static string Root { get; } = FindRoot();

    public static string Preset => Path.Combine(Root, "data", "preset.json");

    public static string Icons => Path.Combine(Root, "data", "icons.json");

    public static string IconPaths => Path.Combine(Root, "data", "icon-paths.json");

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Finance.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Корень репозитория не найден выше {AppContext.BaseDirectory}: нет файла Finance.slnx");
    }
}
