using System.Globalization;
using System.Runtime.CompilerServices;

namespace Finance.Application.Tests;

/// <summary>
/// Культура тестов — русская, как у телефона, для которого приложение и пишется,
/// а не та, что стоит у машины прогона.
/// </summary>
/// <remarks>
/// Разделитель дробной части берётся из культуры устройства (<see cref="Infrastructure.AmountInput.Separator"/>),
/// и тесты набора суммы ждут запятую. На машине с английской культурой — у GitHub Actions
/// это en-US — они падали, хотя приложение исправно. Тест, которому нужна другая культура,
/// ставит её сам вокруг вызова.
/// </remarks>
internal static class TestCulture
{
    /// <summary>
    /// Ставит культуру до первого теста: и текущему потоку, и всем, что появятся потом, —
    /// тесты идут параллельно на потоках пула.
    /// </summary>
    [ModuleInitializer]
    internal static void Set()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
