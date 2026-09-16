using Finance.Application.Features.Places.Catalog;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-05: справочник мест.
/// </summary>
public partial class PlacesPage : DataPage
{
    private readonly PlacesViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления справочника мест.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public PlacesPage(PlacesViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private void OnPlaceTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: PlaceListItem place })
        {
            Guarded.Run(() => Shell.Current.GoToAsync($"{Routes.Place}?key={place.Key}"));
        }
    }
}
