using System.Xml.Linq;

namespace Finance.Docs.Tests;

/// <summary>
/// Сверка знака приложения между файлами, которые рисуют его порознь: слои значка, заставка и плоский силуэт.
/// Общего источника у них нет — сборка рисует каждый файл сама, — и разошедшийся контур или цвет
/// не заметили бы ни сборка, ни экран: значок и заставка видны только на устройстве.
/// </summary>
public sealed class AppIconTests
{
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    // Блик — свой треугольник, обрезанный по стеклу, а не контур знака. Исключается по форме,
    // а не по признаку обрезки: иначе обрезанный контур знака ушёл бы из сверки молча
    private const string Glint = "M60 40 H120 L60 140 Z";

    private static readonly string[] Layers =
    [
        "Resources/AppIcon/appiconfg.svg",
        "Resources/Splash/splash.svg",
        "Resources/Images/about_mark_dark.svg",
        "Resources/Images/about_mark_light.svg",
    ];

    /// <summary>
    /// Контур рубля один во всех слоях: в каждом SVG он повторён без <c>&lt;use&gt;</c>,
    /// и правка чаши в одном файле развела бы тематический значок или заставку с основным знаком.
    /// </summary>
    [Fact]
    public void Контур_знака_один_во_всех_слоях()
    {
        string reference = Silhouette();

        foreach (string file in Layers)
        {
            string[] contours =
            [
                .. XDocument.Load(App(file))
                    .Descendants(Svg + "path")
                    .Select(static path => path.Attribute("d")!.Value)
                    .Where(static d => d != Glint),
            ];

            // Без контуров сверка прошла бы молча — например, если файл сменит пространство имён
            Assert.NotEmpty(contours);
            Assert.All(contours, contour => Assert.True(
                contour == reference,
                $"{file}: контур разошёлся с силуэтом appicon_monochrome.xml\n  слой:    {contour}\n  силуэт:  {reference}"));
        }
    }

    /// <summary>
    /// Движущаяся заставка нарисована тем же контуром: она векторная и повторяет знак слоями, как SVG.
    /// </summary>
    [Fact]
    public void Контур_движущейся_заставки_тот_же()
    {
        XDocument splash = XDocument.Load(App("Platforms/Android/Resources/drawable/splash_echo.xml"));
        string reference = Silhouette();

        string[] contours =
        [
            .. splash.Descendants()
                .Where(static element => element.Name.LocalName is "path" or "clip-path")
                // Отсвет — своя фигура, а не контур знака
                .Where(static element => (string?)element.Attribute(Android + "name") is not "halo")
                .Select(static element => element.Attribute(Android + "pathData")!.Value)
                .Where(static data => data != Glint),
        ];

        Assert.NotEmpty(contours);
        Assert.All(contours, contour => Assert.True(
            contour == reference,
            $"splash_echo.xml: контур разошёлся с силуэтом appicon_monochrome.xml\n  слой:    {contour}\n  силуэт:  {reference}"));
    }

    /// <summary>
    /// Каждая анимация заставки находит, что двигать: переименованная группа или путь оставили бы
    /// эхо неподвижным, а сборка и экран без движения об этом промолчали бы.
    /// </summary>
    [Fact]
    public void Анимации_заставки_находят_свои_слои()
    {
        XDocument splash = XDocument.Load(App("Platforms/Android/Resources/drawable/splash_echo.xml"));

        HashSet<string> named =
        [
            .. splash.Descendants()
                .Where(static element => element.Name.LocalName is "group" or "path")
                .Select(static element => (string?)element.Attribute(Android + "name"))
                .OfType<string>(),
        ];
        string[] targets =
        [
            .. splash.Descendants("target").Select(static target => target.Attribute(Android + "name")!.Value),
        ];

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.Contains(target, named));
    }

    /// <summary>
    /// Фон заставки — цвет верха фона экрана ПИН-кода: сменённый токен темы без правки проекта
    /// дал бы скачок оттенка при переходе с заставки на первый экран.
    /// </summary>
    [Fact]
    public void Заставка_в_цвет_фона_экрана_ПИН_кода()
    {
        string splash = XDocument.Load(App("Finance.App.csproj"))
            .Descendants("MauiSplashScreen")
            .Single()
            .Attribute("Color")!.Value;

        string backdrop = XDocument.Load(App("Resources/Styles/Colors.xaml"))
            .Root!
            .Elements()
            .Single(static element => (string?)element.Attribute(Xaml + "Key") == "BackdropTopDark")
            .Value;

        Assert.Equal(backdrop, splash, ignoreCase: true);
    }

    /// <summary>
    /// Контур знака, с которым сверяются остальные слои: плоский силуэт тематического значка.
    /// </summary>
    private static string Silhouette() => XDocument.Load(App("Platforms/Android/Resources/drawable/appicon_monochrome.xml"))
        .Descendants("path")
        .Single()
        .Attribute(Android + "pathData")!.Value;

    private static string App(string relative) => Path.Combine(Repository.Root, "src", "Finance.App", relative);
}
