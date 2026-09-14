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

    private void OnCreateAccount(object? sender, EventArgs e) =>
        Guarded.Run(() => Shell.Current.GoToAsync(Routes.Account));

    // Перетаскиваемая строка запоминается здесь, а не передаётся через данные
    // жеста: те сериализуются платформой, а нужна ссылка на строку модели
    private AccountRowItem? _dragged;

    private void OnDragStarting(object? sender, DragStartingEventArgs e) =>
        _dragged = RowOf(sender);

    private void OnDrop(object? sender, DropEventArgs e) => Guarded.Run(() => MoveAsync(RowOf(sender)));

    private async Task MoveAsync(AccountRowItem? target)
    {
        // Сброс в finally: сорвавшийся перенос оставил бы строку «перетаскиваемой»,
        // и следующее перетаскивание двинуло бы не ту
        try
        {
            if (_dragged is { } dragged && target is not null)
            {
                await _model.MoveAsync(dragged, target);
            }
        }
        finally
        {
            _dragged = null;
        }
    }

    private static AccountRowItem? RowOf(object? sender) =>
        sender is GestureRecognizer { Parent: BindableObject { BindingContext: AccountRowItem row } } ? row : null;

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: AccountRowItem account })
        {
            Guarded.Run(() => Shell.Current.GoToAsync($"{Routes.Account}?key={account.Key}"));
        }
    }
}
