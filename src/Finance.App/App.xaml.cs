using Finance.Application.Infrastructure;

namespace Finance.App;

/// <summary>
/// Приложение: ресурсы темы и корневое окно.
/// </summary>
public sealed partial class App : ControlsApplication
{
    private readonly TimeZoneFollower _zone;

    /// <summary>
    /// Создаёт приложение.
    /// </summary>
    /// <param name="zone">Следит за часовым поясом телефона при возврате из фона.</param>
    public App(TimeZoneFollower zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        InitializeComponent();

        _zone = zone;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new AppShell());

        // Возврат из фона, а не каждое получение фокуса: шторка уведомлений и диалог
        // фокус отнимают, но пояс за это время не меняется. Resumed приходит раньше,
        // чем экраны появятся и решат, устарели ли они, — «сегодня» к тому времени новое
        window.Resumed += OnResumed;

        return window;
    }

    private void OnResumed(object? sender, EventArgs e) => _zone.Resume();
}
