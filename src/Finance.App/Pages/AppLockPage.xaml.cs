using Finance.Application.Features.Settings.AppLock;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-14: «Защита входа».
/// </summary>
public sealed partial class AppLockPage : ContentPage
{
    private readonly AppLockViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель экрана защиты входа.</param>
    public AppLockPage(AppLockViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Состояние перечитывается при каждом появлении: экран ПИН-кода меняет его, уходя.
    /// Базы защита не касается, поэтому и подготовки базы ждать не нужно.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _model.Load();
    }

    private void OnCreate(object? sender, TappedEventArgs e) => Go(PinPurpose.Create);

    private void OnChange(object? sender, TappedEventArgs e) => Go(PinPurpose.Change);

    private void OnDisable(object? sender, TappedEventArgs e) => Go(PinPurpose.Disable);

    private static void Go(PinPurpose purpose) =>
        Navigator.Go($"{Routes.Pin}?purpose={purpose}");
}
