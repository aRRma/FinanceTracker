using Microsoft.Maui.Controls.Shapes;

namespace Finance.App.Controls;

/// <summary>
/// Атмосфера — фон экрана в стиле «Изумруд»: переход сверху вниз и три цветных пятна,
/// медленно плывущих взад и вперёд. Ставится слоем под содержимым экрана.
/// </summary>
/// <remarks>
/// Размытия заднего плана в MAUI нет, и оно не нужно: пятно — эллипс с радиальным
/// переходом от цвета к прозрачному, гладкое само по себе. Место и размер пятен — доли
/// ширины и высоты, чтобы рисунок не зависел от размера экрана. С выключенными в системе
/// анимациями пятна стоят на месте. Приёмы стиля — в docs/ui/style.md.
/// </remarks>
public sealed class Backdrop : Microsoft.Maui.Controls.AbsoluteLayout
{
    /// <summary>
    /// Место середины перехода по высоте: верх экрана цветной, низ под клавиатурой спокойный.
    /// Общее с подложкой окна заслонки — иначе при её смене середина прыгала бы цветом.
    /// </summary>
    internal const float MiddleStop = 0.42f;

    /// <summary>
    /// Доля яркости пятен на приглушённом фоне.
    /// </summary>
    private const double DimmedOpacity = 0.4;

    /// <summary>
    /// Свойство <see cref="IsDimmed"/>.
    /// </summary>
    public static readonly BindableProperty IsDimmedProperty = BindableProperty.Create(
        nameof(IsDimmed),
        typeof(bool),
        typeof(Backdrop),
        defaultValue: false,
        propertyChanged: static (bindable, _, value) => ((Backdrop)bindable).Dim((bool)value));

    // Тон, плотность в светлой и тёмной теме, центр и поперечник долями экрана,
    // размах дрейфа и прирост размера в крайней точке, полный круг в секундах.
    // Периоды разные и не кратные: пятна не возвращаются в исходный рисунок разом
    private static readonly Blob[] Blobs =
    [
        new("AccountSky", Light: 0.50, Dark: 0.32, X: 0.12, Y: 0.12, Size: 1.27, DriftX: 0.18, DriftY: 0.07, Grow: 0.12, Seconds: 19),
        new("AccountGreen", Light: 0.38, Dark: 0.26, X: 0.94, Y: 0.49, Size: 1.15, DriftX: -0.21, DriftY: 0.06, Grow: -0.10, Seconds: 23),
        new("Accent", Light: 0.16, Dark: 0.18, X: 0.12, Y: 0.77, Size: 1.21, DriftX: 0.21, DriftY: -0.07, Grow: 0.15, Seconds: 21)
    ];

    private readonly List<(Ellipse Shape, Blob Blob)> _blobs = [];

    /// <summary>
    /// Создаёт атмосферу.
    /// </summary>
    public Backdrop()
    {
        InputTransparent = true;

        // Пятна шире экрана и уходят за его края: без обрезки они легли бы
        // на шапку страницы и на соседние виды
        IsClippedToBounds = true;

        if (Gradient("Light") is { } light && Gradient("Dark") is { } dark)
        {
            this.SetAppTheme<Brush>(BackgroundProperty, light, dark);
        }

        foreach (Blob blob in Blobs)
        {
            if (Palette.Find($"{blob.Tone}Light") is not { } lightTone || Palette.Find($"{blob.Tone}Dark") is not { } darkTone)
            {
                continue;
            }

            Ellipse shape = new() { InputTransparent = true };

            shape.SetAppTheme<Brush>(Shape.FillProperty, Spot(lightTone, blob.Light), Spot(darkTone, blob.Dark));

            Children.Add(shape);
            _blobs.Add((shape, blob));
        }

        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Пятна приглушены — на время паузы после неверных попыток, вместе с клавиатурой.
    /// </summary>
    public bool IsDimmed
    {
        get => (bool)GetValue(IsDimmedProperty);
        set => SetValue(IsDimmedProperty, value);
    }

    /// <summary>
    /// Три точки перехода сверху вниз в теме. Нет токена — <see langword="null"/>.
    /// </summary>
    /// <param name="theme">Суффикс темы токенов.</param>
    internal static Color[]? Tones(string theme) =>
        Palette.Find($"BackdropTop{theme}") is { } top
        && Palette.Find($"BackdropMiddle{theme}") is { } middle
        && Palette.Find($"BackdropBottom{theme}") is { } bottom
            ? [top, middle, bottom]
            : null;

    /// <summary>
    /// Отключение обработчиков тоже останавливает дрейф. Заслонку убирают закрытием
    /// окна и сразу отключают обработчики, и ухода из окна атмосфера может не
    /// услышать — бесконечные анимации держали бы её в памяти после каждого входа.
    /// </summary>
    /// <param name="args">Прежний и новый обработчик.</param>
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        base.OnHandlerChanging(args);

        if (args.NewHandler is null)
        {
            StopDrift();
        }
    }

    private static LinearGradientBrush? Gradient(string theme) =>
        Tones(theme) is [var top, var middle, var bottom]
            ? new LinearGradientBrush(
                [new GradientStop(top, 0f), new GradientStop(middle, MiddleStop), new GradientStop(bottom, 1f)],
                new Point(0, 0),
                new Point(0, 1))
            : null;

    /// <summary>
    /// Пятно: цвет с плотностью в середине, к краю — тот же цвет, но прозрачный.
    /// Переход к прозрачному белому дал бы светлую кайму в тёмной теме.
    /// </summary>
    private static RadialGradientBrush Spot(Color tone, double density) =>
        new([new GradientStop(tone.WithAlpha((float)density), 0f), new GradientStop(tone.WithAlpha(0f), 1f)], new Point(0.5, 0.5), 0.5);

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        foreach ((Ellipse shape, Blob blob) in _blobs)
        {
            double side = blob.Size * Width;

            SetLayoutBounds((BindableObject)shape, new Rect((blob.X * Width) - (side / 2), (blob.Y * Height) - (side / 2), side, side));
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (!Motion.IsOn)
        {
            return;
        }

        foreach ((Ellipse shape, Blob blob) in _blobs)
        {
            // Плавно туда и обратно за один круг: половина косинуса без рывка в крайних точках
            Animation drift = new(
                progress =>
                {
                    double swing = (1 - Math.Cos(2 * Math.PI * progress)) / 2;

                    shape.TranslationX = blob.DriftX * Width * swing;
                    shape.TranslationY = blob.DriftY * Height * swing;
                    shape.Scale = 1 + (blob.Grow * swing);
                });

            drift.Commit(shape, nameof(Backdrop), length: (uint)(blob.Seconds * 1000), easing: Easing.Linear, repeat: static () => true);
        }
    }

    private void OnUnloaded(object? sender, EventArgs e) => StopDrift();

    private void StopDrift()
    {
        foreach ((Ellipse shape, _) in _blobs)
        {
            shape.AbortAnimation(nameof(Backdrop));
        }
    }

    private void Dim(bool dimmed)
    {
        double opacity = dimmed ? DimmedOpacity : 1;

        foreach ((Ellipse shape, _) in _blobs)
        {
            // Признак приходит привязкой и до показа — тогда без анимации
            if (shape.Handler is null)
            {
                shape.Opacity = opacity;
            }
            else
            {
                Guarded.Run(() => shape.FadeToAsync(opacity, 300));
            }
        }
    }

    /// <summary>
    /// Пятно атмосферы.
    /// </summary>
    private readonly record struct Blob(
        string Tone,
        double Light,
        double Dark,
        double X,
        double Y,
        double Size,
        double DriftX,
        double DriftY,
        double Grow,
        double Seconds);
}
