using Finance.Application.Features.Settings.Appearance;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-06: выбор темы оформления.
/// </summary>
public partial class AppearancePage : DataPage
{
    private readonly AppearanceViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана оформления.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AppearancePage(AppearanceViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ThemeOption option })
        {
            Guarded.Run(() => _model.SelectAsync(option.Theme));
        }
    }
}
