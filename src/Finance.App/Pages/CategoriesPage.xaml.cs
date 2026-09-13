using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран D-03: справочник категорий.</summary>
public partial class CategoriesPage : DataPage
{
    private readonly CategoriesViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления справочника категорий.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public CategoriesPage(CategoriesViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private async void OnCreateGroup(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(Routes.Group);

    private async void OnGroupTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: CategoryGroupItem group })
        {
            await Shell.Current.GoToAsync($"{Routes.Group}?key={group.Key}");
        }
    }

    // «Прочее» и служебная не открываются: переносить и удалять их нельзя,
    // а карточка без единого доступного действия сбивает с толку
    private async void OnSubcategoryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: CategoryRowItem { IsEditable: true } subcategory })
        {
            await Shell.Current.GoToAsync($"{Routes.Subcategory}?key={subcategory.Key}");
        }
    }
}
