using Finance.Application.Features.Accounts.Catalog;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран D-01: справочник счетов.</summary>
public partial class AccountsPage : DataPage
{
    private readonly AccountsViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления справочника счетов.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AccountsPage(AccountsViewModel model, FinanceStartup startup)
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

    // Перетаскиваемая строка запоминается здесь, а не передаётся через данные
    // жеста: те сериализуются платформой, а нужна ссылка на строку модели
    private AccountRowItem? _dragged;

    private void OnDragStarting(object? sender, DragStartingEventArgs e) =>
        _dragged = RowOf(sender);

    private async void OnDrop(object? sender, DropEventArgs e)
    {
        if (_dragged is { } dragged && RowOf(sender) is { } target)
        {
            await _model.MoveAsync(dragged, target);
        }

        _dragged = null;
    }

    private static AccountRowItem? RowOf(object? sender) =>
        sender is GestureRecognizer { Parent: BindableObject { BindingContext: AccountRowItem row } } ? row : null;

    private async void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: AccountRowItem account })
        {
            await Shell.Current.GoToAsync($"{Routes.Account}?key={account.Key}");
        }
    }
}
