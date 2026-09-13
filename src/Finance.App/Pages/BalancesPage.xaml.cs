using Finance.Application.Features.Balances;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран A-01: балансы счетов и «доступно к тратам» по валютам.</summary>
public partial class BalancesPage : DataPage
{
    private readonly BalancesViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления главного экрана.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public BalancesPage(BalancesViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private async void OnCreateAccount(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(Routes.Account);
}
