namespace Finance.App;

/// <summary>Приложение: ресурсы темы и корневое окно.</summary>
public partial class App : ControlsApplication
{
    /// <summary>Создаёт приложение.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell());
}
