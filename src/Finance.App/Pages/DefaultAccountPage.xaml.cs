using Finance.Application.Features.Settings.DefaultAccounts;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-13: выбор счёта по умолчанию.
/// </summary>
public sealed partial class DefaultAccountPage : DataPage
{
    private readonly DefaultAccountViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана счёта по умолчанию.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public DefaultAccountPage(DefaultAccountViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: DefaultAccountOption option })
        {
            Guarded.Run(() => _model.SelectAsync(option));
        }
    }
}
