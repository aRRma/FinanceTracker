using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Microsoft.Maui.Controls.Shapes;

namespace Finance.App.Controls;

/// <summary>
/// Знак счёта — скруглённый квадрат, залитый цветом счёта, со значком. Один контрол
/// на плашку, шапку и жетон: разные размеры одного знака, и разойдись они — счёт
/// выглядел бы на балансах и в ленте по-разному.
/// </summary>
/// <remarks>
/// Цвет берётся из словаря темы по имени <c>Account{Цвет}Light</c>/<c>Dark</c> и
/// привязан к теме платформой: смена темы перекрашивает знак без подписки. TalkBack
/// знак не читает — он повторяет название рядом.
/// </remarks>
public sealed class AccountBadge : ContentView
{
    /// <summary>
    /// Цвет счёта.
    /// </summary>
    public static readonly BindableProperty ToneProperty = BindableProperty.Create(
        nameof(Tone),
        typeof(AccountColor),
        typeof(AccountBadge),
        AccountColor.Blue,
        propertyChanged: static (bindable, _, _) => ((AccountBadge)bindable).Paint());

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public static readonly BindableProperty IconKeyProperty = BindableProperty.Create(
        nameof(IconKey),
        typeof(string),
        typeof(AccountBadge),
        propertyChanged: static (bindable, _, value) =>
        {
            // Без ключа — одна заливка: так рисуется ячейка невыбранного цвета
            Icon icon = ((AccountBadge)bindable)._icon;
            icon.Key = (string?)value;
            icon.IsVisible = value is not null;
        });

    /// <summary>
    /// Размер знака.
    /// </summary>
    public static readonly BindableProperty ModeProperty = BindableProperty.Create(
        nameof(Mode),
        typeof(AccountBadgeMode),
        typeof(AccountBadge),
        AccountBadgeMode.Tile,
        propertyChanged: static (bindable, _, _) => ((AccountBadge)bindable).ApplyMode());

    private readonly Border _fill;
    private readonly Icon _icon;

    /// <summary>
    /// Создаёт знак синего цвета размером плашки; цвет, значок и размер задаются привязкой.
    /// </summary>
    public AccountBadge()
    {
        _icon = new Icon { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, IsVisible = false };
        _fill = new Border { StrokeThickness = 0, Padding = 0, Content = _icon };

        Content = _fill;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Start;
        AutomationProperties.SetIsInAccessibleTree(this, false);

        ApplyMode();
        Paint();
    }

    /// <summary>
    /// Цвет счёта.
    /// </summary>
    public AccountColor Tone
    {
        get => (AccountColor)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>
    /// Ключ значка: выбранный руками или по типу счёта.
    /// </summary>
    public string? IconKey
    {
        get => (string?)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    /// <summary>
    /// Плашка, шапка или жетон.
    /// </summary>
    public AccountBadgeMode Mode
    {
        get => (AccountBadgeMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>
    /// Сторона, скругление, значок и его линия по месту знака. Линия тоньше набора
    /// пропорционально значку: на 13 точках линия 2 слила бы знак в пятно.
    /// </summary>
    private void ApplyMode()
    {
        (double side, double radius, double icon, double stroke) = Mode switch
        {
            AccountBadgeMode.Header => (44d, 13d, 25d, 2d),
            AccountBadgeMode.Token => (19d, 6d, 13d, 1.3d),
            _ => (36d, 11d, 22d, 1.85d)
        };

        WidthRequest = side;
        HeightRequest = side;
        _fill.StrokeShape = new RoundRectangle { CornerRadius = radius };
        _icon.Size = icon;
        _icon.StrokeThickness = stroke;
    }

    private void Paint()
    {
        string name = $"Account{Tone}";

        if (Resource($"{name}Light") is { } light && Resource($"{name}Dark") is { } dark)
        {
            _fill.SetAppThemeColor(BackgroundColorProperty, light, dark);
        }

        _icon.Stroke = Resource(AccountColors.TakesDarkGlyph(Tone) ? "AccountGlyphInk" : "AccountGlyphWhite") is { } glyph
            ? new SolidColorBrush(glyph)
            : Brush.White;
    }

    // Unknown и цвет без токена остаются без заливки: так пропуск в палитре виден сразу
    private static Color? Resource(string key) =>
        ControlsApplication.Current?.Resources.TryGetValue(key, out object? value) is true ? value as Color : null;
}
