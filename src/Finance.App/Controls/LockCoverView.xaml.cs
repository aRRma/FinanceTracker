using Finance.Application.Features.AppLock;

namespace Finance.App.Controls;

/// <summary>
/// Содержимое заслонки: набор ПИН-кода во весь экран.
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
    /// Готовит к показу: чистый набор и отсчёт идущей паузы.
    /// </summary>
    public void Prepare()
    {
        _model.Prepare();
        Pad.Resume();
    }
}
