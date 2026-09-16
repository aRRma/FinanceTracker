using Finance.Application.Features.Settings.About;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-09: «О программе».
/// </summary>
public partial class AboutPage : DataPage
{
    private readonly AboutViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана «О программе».</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AboutPage(AboutViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();
}
