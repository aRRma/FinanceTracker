using Finance.Application.Features.More;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран A-04: справочники и настройки.
/// </summary>
public sealed partial class MorePage : DataPage
{
    private readonly MoreViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
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

    private void OnAccounts(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.Accounts);

    private void OnPlaces(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.Places);

    private void OnCategories(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.Categories);

    private void OnAppearance(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.Appearance);

    private void OnTimeZone(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.TimeZone);

    private void OnDefaultAccount(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.DefaultAccount);

    private void OnAppLock(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.AppLock);

    private void OnExport(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.Export);

    private void OnAbout(object? sender, TappedEventArgs e) =>
        Navigator.Go(Routes.About);
}
