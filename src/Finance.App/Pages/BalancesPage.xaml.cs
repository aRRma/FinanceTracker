using Finance.Application.Features.Balances;
using Finance.Application.Features.Settings.AppLock;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.AppLock;
using Finance.Application.Texts;

namespace Finance.App.Pages;

/// <summary>
/// Экран A-01: балансы счетов и «доступно к тратам» по валютам.
/// </summary>
public sealed partial class BalancesPage : DataPage
{
    private readonly BalancesViewModel _model;
    private readonly AppLockOffer _offer;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления главного экрана.</param>
    /// <param name="offer">Предложение защитить вход при первом запуске.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public BalancesPage(BalancesViewModel model, AppLockOffer offer, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(offer);

        InitializeComponent();

        _model = model;
        _offer = offer;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override async Task LoadAsync()
    {
        await _model.LoadAsync();

        // Первый экран после установки: подготовка уже прошла, и известно,
        // создана ли база этим запуском. Вопрос встаёт над экраном первого
        // запуска, до того как пользователь заведёт счёт
        if (_offer.Take()
            && await DisplayAlertAsync(UiTexts.AppLockOfferTitle, null, UiTexts.AppLockCreate, UiTexts.AppLockOfferLater))
        {
            await Navigator.GoAsync($"{Routes.Pin}?purpose={PinPurpose.Create}");
        }
    }

    private void OnCreateAccount(object? sender, EventArgs e) =>
        Navigator.Go(Routes.Account);

    private void OnRecover(object? sender, EventArgs e) =>
        Navigator.Go(Routes.Export);

    private void OnAddTransaction(object? sender, EventArgs e) =>
        Navigator.Go(Routes.Transaction);

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: AccountTile account })
        {
            Navigator.Go($"{Routes.AccountFeed}?key={account.Key}");
        }
    }
}
