using Finance.Application.Features.Settings.Diagnostics;
using Microsoft.Maui.Controls.Shapes;

namespace Finance.App.Controls;

/// <summary>
/// Знак вида сбоя — скруглённый квадрат с подложкой цвета вида, тонкой кромкой того же цвета
/// и значком в одну линию. Тихий, без заливки: на экране отчётов он подсказывает вид, а не кричит.
/// </summary>
/// <remarks>
/// Цвета — токены <c>Crash{Вид}</c> и <c>Crash{Вид}Tint</c> обеих тем, привязанные к теме платформой.
/// Кромка — цвет значка на просвет: декоративная, в контрасте не участвует. TalkBack знак не читает —
/// рядом стоит заголовок вида.
/// </remarks>
public sealed class CrashBadge : ContentView
{
    /// <summary>
    /// Вид сбоя.
    /// </summary>
    public static readonly BindableProperty CategoryProperty = BindableProperty.Create(
        nameof(Category),
        typeof(CrashCategory),
        typeof(CrashBadge),
        CrashCategory.ActionFailed,
        propertyChanged: static (bindable, _, _) => ((CrashBadge)bindable).Paint());

    /// <summary>
    /// Крупный знак — для шапки экрана отчёта.
    /// </summary>
    public static readonly BindableProperty IsLargeProperty = BindableProperty.Create(
        nameof(IsLarge),
        typeof(bool),
        typeof(CrashBadge),
        false,
        propertyChanged: static (bindable, _, _) => ((CrashBadge)bindable).ApplySize());

    private readonly Border _fill;
    private readonly Icon _icon;

    /// <summary>
    /// Создаёт знак; вид и размер задаются привязкой.
    /// </summary>
    public CrashBadge()
    {
        _icon = new Icon { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _fill = new Border { StrokeThickness = 1, Padding = 0, Content = _icon };

        Content = _fill;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Start;
        AutomationProperties.SetIsInAccessibleTree(this, false);

        ApplySize();
        Paint();
    }

    /// <summary>
    /// Вид сбоя.
    /// </summary>
    public CrashCategory Category
    {
        get => (CrashCategory)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    /// <summary>
    /// Крупный знак.
    /// </summary>
    public bool IsLarge
    {
        get => (bool)GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    /// <summary>
    /// Сторона, скругление и значок; линия у крупного тоньше пропорционально, как у знака счёта.
    /// </summary>
    private void ApplySize()
    {
        (double side, double radius, double icon, double stroke) = IsLarge ? (46d, 15d, 24d, 1.6d) : (30d, 10d, 16d, 1.75d);

        WidthRequest = side;
        HeightRequest = side;
        _fill.StrokeShape = new RoundRectangle { CornerRadius = radius };
        _icon.Size = icon;
        _icon.StrokeThickness = stroke;
    }

    private void Paint()
    {
        (string tone, string key) = Category switch
        {
            CrashCategory.AppClosed => ("Closed", "bolt"),
            CrashCategory.SystemFault => ("System", "cpu"),
            CrashCategory.Froze => ("Froze", "hourglass"),
            CrashCategory.Killed => ("Killed", "moon"),
            CrashCategory.Warning => ("Warning", "alert-triangle"),
            _ => ("Caught", "alert-circle"),
        };

        _icon.Key = key;

        // Токен без пары тем оставляет знак без цвета: пропуск в палитре виден сразу
        if (Palette.Find($"Crash{tone}Light") is not { } light || Palette.Find($"Crash{tone}Dark") is not { } dark
            || Palette.Find($"Crash{tone}TintLight") is not { } tintLight || Palette.Find($"Crash{tone}TintDark") is not { } tintDark)
        {
            return;
        }

        _fill.SetAppThemeColor(BackgroundColorProperty, tintLight, tintDark);
        _fill.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(light.WithAlpha(0.28f)), new SolidColorBrush(dark.WithAlpha(0.28f)));
        _icon.SetAppTheme<Brush>(Shape.StrokeProperty, new SolidColorBrush(light), new SolidColorBrush(dark));
    }
}
