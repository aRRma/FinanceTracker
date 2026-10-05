using System.Numerics;
using Microsoft.Maui.Controls.Shapes;

namespace Finance.App.Controls;

/// <summary>
/// Значок по ключу из набора. Контур рисуется линией, а не заливкой, поэтому цвет
/// задаётся через <see cref="Shape.Stroke"/> — токеном темы из стиля, как у текста.
/// </summary>
/// <remarks>
/// Наследует <see cref="Shape"/>, а не оборачивает <see cref="Microsoft.Maui.Controls.Shapes.Path"/>:
/// тот запечатан, а обёртка добавила бы лишний уровень разметки в каждую строку ленты.
/// </remarks>
public sealed class Icon : Shape
{
    /// <summary>
    /// Сторона значка в единицах контура: набор нарисован в сетке 24×24.
    /// </summary>
    private const double Side = 24;

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public static readonly BindableProperty KeyProperty = BindableProperty.Create(
        nameof(Key),
        typeof(string),
        typeof(Icon),
        propertyChanged: static (bindable, _, value) => ((Icon)bindable).Redraw((string?)value));

    /// <summary>
    /// Сторона значка на экране.
    /// </summary>
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size),
        typeof(double),
        typeof(Icon),
        Side,
        propertyChanged: static (bindable, _, value) => ((Icon)bindable).Resize((double)value));

    private PathF _path = IconGeometry.For(null);

    /// <summary>
    /// Создаёт значок с размером и линией набора; ключ задаётся привязкой.
    /// </summary>
    public Icon()
    {
        WidthRequest = Side;
        HeightRequest = Side;
        StrokeThickness = 2;
        StrokeLineCap = PenLineCap.Round;
        StrokeLineJoin = PenLineJoin.Round;
    }

    /// <summary>
    /// Ключ значка из набора. Неизвестный рисуется запасным.
    /// </summary>
    public string? Key
    {
        get => (string?)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>
    /// Сторона значка на экране; по умолчанию — сетка набора, 24. Контур масштабируется
    /// целиком, сетка остаётся сеткой: растяжка по границам фигуры (<c>Aspect</c>)
    /// подогнала бы каждый знак под свои края, и узкие вышли бы крупнее широких.
    /// </summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Копия, а не общий контур: при отрисовке MAUI подгоняет путь под границы
    /// вида на месте, и общий экземпляр накапливал бы чужие преобразования.
    /// </remarks>
    public override PathF GetPath()
    {
        PathF path = new(_path);

        if (Size is not Side)
        {
            path.Transform(Matrix3x2.CreateScale((float)(Size / Side)));
        }

        return path;
    }

    private void Resize(double size)
    {
        WidthRequest = size;
        HeightRequest = size;
        Handler?.UpdateValue(nameof(IShapeView.Shape));
    }

    private void Redraw(string? key)
    {
        _path = IconGeometry.For(key);

        // Обработчик перерисовывает фигуру только по сигналу: сам он о смене контура не узнает
        Handler?.UpdateValue(nameof(IShapeView.Shape));
    }
}
