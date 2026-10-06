using Finance.Application.Features.AppLock;

namespace Finance.App.Controls;

/// <summary>
/// Содержимое заслонки: набор ПИН-кода во весь экран на атмосфере.
/// </summary>
public sealed partial class LockCoverView : ContentView
{
    private readonly LockCoverViewModel _model;

    /// <summary>
    /// Создаёт содержимое заслонки.
    /// </summary>
    /// <param name="model">Модель заслонки.</param>
    public LockCoverView(LockCoverViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Готовит к показу: чистый набор, отсчёт идущей паузы и появление с анимацией.
    /// </summary>
    public void Prepare()
    {
        _model.Prepare();
        Pad.Resume();
        Pad.Enter();
    }

    /// <summary>
    /// Готовит к показу с тем же набором — заслонку пересобрали при смене темы.
    /// Без анимации появления: набор уже был на экране.
    /// </summary>
    public void Resume() => Pad.Resume();

    /// <summary>
    /// Отступ набора от краёв под системные полосы; атмосфера остаётся под ними.
    /// </summary>
    /// <param name="bars">Отступы в независимых точках.</param>
    public void Inset(Thickness bars) => Pad.Margin = bars;

    /// <summary>
    /// Уход после верного кода: замок открывается, точки сходятся, и заслонка
    /// растворяется, приближаясь, — под ней уже экран приложения.
    /// </summary>
    /// <remarks>
    /// Касания на это время не принимаются: набор, начатый в долю секунды до
    /// ухода, ушёл бы в никуда.
    /// </remarks>
    /// <param name="reveal">Открывает экран приложения под заслонкой — перед тем как она растворится.</param>
    public async Task LeaveAsync(Action reveal)
    {
        ArgumentNullException.ThrowIfNull(reveal);

        InputTransparent = true;

        await Pad.OpenAsync();
        reveal();
        await Task.WhenAll(Root.FadeToAsync(0, 380, Easing.CubicInOut), Root.ScaleToAsync(1.08, 380, Easing.CubicInOut));
    }
}
