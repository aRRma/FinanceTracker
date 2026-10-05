using Finance.Application.Features.Settings.AppLock;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-15: ПИН-код — задание, смена, выключение защиты.
/// </summary>
[QueryProperty(nameof(Purpose), "purpose")]
public sealed partial class PinPage : ContentPage
{
    private readonly PinViewModel _model;

    private bool _started;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель экрана ПИН-кода.</param>
    public PinPage(PinViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        _model.Done += OnDone;
        BindingContext = model;
    }

    /// <summary>
    /// Зачем открыт экран — имя члена <see cref="PinPurpose"/> из маршрута.
    /// </summary>
    public string? Purpose { get; set; }

    /// <summary>
    /// Первый шаг — при первом появлении, когда параметр маршрута уже пришёл.
    /// Возврат из фона набранное не стирает.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
        {
            return;
        }

        _started = true;

        _model.Start(Enum.TryParse(Purpose, out PinPurpose purpose) ? purpose : PinPurpose.Create);
        Pad.Resume();
    }

    private void OnDone(object? sender, string text)
    {
        Notice.Show(text);
        Navigator.Go("..");
    }
}
