using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Finance.Docs.Tests;

/// <summary>
/// Прототип собран из нынешних макетов. Он генерируется скриптом и руками не правится,
/// поэтому разойтись с макетами может только одним способом — если после правки
/// макетов сборку забыли запустить.
/// </summary>
/// <remarks>
/// Сверяются отпечатки, которые скрипт проставляет в собранный файл: запускать Python
/// из тестов нельзя — его наличие и имя различаются на разных машинах, а проверка
/// обязана работать везде, где идёт <c>dotnet test</c>.
/// </remarks>
public sealed partial class PrototypeFreshnessTests
{
    /// <summary>
    /// Прототип пересобран после последней правки макетов и самого сборщика.
    /// </summary>
    [Fact]
    public void Прототип_собран_из_нынешних_макетов()
    {
        Match stamp = Stamp().Match(File.ReadAllText(Documents.Prototype));

        Assert.True(stamp.Success, "в prototype.html нет отпечатков: запустите python tools/build_prototype.py");

        // Сравниваются отпечатки, а сообщение называет файл: увидев два разных
        // набора из шестидесяти четырёх букв, читатель не поймёт, что делать
        Assert.True(
            stamp.Groups["mockups"].Value == Fingerprint(Documents.Mockups),
            "макеты правились после сборки прототипа: запустите python tools/build_prototype.py");

        Assert.True(
            stamp.Groups["builder"].Value == Fingerprint(Documents.Builder),
            "сборщик правился после сборки прототипа: запустите python tools/build_prototype.py");
    }

    /// <summary>
    /// Отпечаток считается по тексту и зависит от него. Считай он по чему-то ещё —
    /// сверка выше сравнивала бы две константы и проходила бы всегда.
    /// </summary>
    [Fact]
    public void Отпечаток_меняется_вместе_с_файлом()
    {
        Assert.NotEqual(Fingerprint(Documents.Mockups), Fingerprint(Documents.Builder));
        Assert.Equal(64, Fingerprint(Documents.Mockups).Length);
    }

    /// <summary>
    /// Отпечатки источников, проставленные сборщиком.
    /// </summary>
    [GeneratedRegex("""<!-- отпечатки источников: mockups\.html=(?<mockups>[0-9a-f]{64}) build_prototype\.py=(?<builder>[0-9a-f]{64}) -->""")]
    private static partial Regex Stamp();

    // Переводы строк приводятся к одному виду, а метка порядка байтов отбрасывается
    // чтением: иначе отпечаток зависел бы от настроек checkout и расходился бы
    // у второго разработчика на ровном месте. Тот же расчёт — в сборщике
    private static string Fingerprint(string file)
    {
        string text = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
