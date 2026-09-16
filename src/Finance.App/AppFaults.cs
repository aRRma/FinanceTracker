namespace Finance.App;

/// <summary>
/// Тексты сбоев самого приложения: ресурс не вшит в сборку, у значка нет контура.
/// Пишутся тому, кто читает журнал, а не пользователю — <see cref="Guarded"/>
/// показывает наружу только доменные правила и сорванную миграцию.
/// </summary>
internal static class AppFaults
{
    /// <summary>
    /// Встроенный в сборку ресурс не найден: сборка собрана без него.
    /// </summary>
    /// <param name="name">Имя ресурса.</param>
    internal static string ResourceMissing(string name) => $"Ресурс {name} не вшит в сборку";

    /// <summary>
    /// У ключа из набора значков нет контура. Сверяется тестом, поэтому сюда
    /// попадает только рассогласование сборки с данными.
    /// </summary>
    /// <param name="key">Ключ значка.</param>
    internal static string IconPathMissing(string key) => $"У значка «{key}» нет контура";
}
