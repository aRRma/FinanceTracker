using Finance.Application.Features.More;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран A-04: справочники и настройки.</summary>
public partial class MorePage : DataPage
{
    private readonly MoreViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления раздела «Ещё».</param>
    /// <param name="startup">Подготовка приложения.</param>
    public MorePage(MoreViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private async void OnAccounts(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(Routes.Accounts);

    private async void OnCategories(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(Routes.Categories);
}
