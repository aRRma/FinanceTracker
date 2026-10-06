using Finance.Application.Features.Categories.Card;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экраны D-04 и D-07: карточка группы — заведение и правка.
/// </summary>
[QueryProperty(nameof(Key), "key")]
public sealed partial class GroupPage : DataPage
{
    private readonly GroupViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления карточки группы.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public GroupPage(GroupViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Ключ правимой группы из маршрута. Пусто — заводится новая.
    /// Строкой, а не <see cref="Guid"/>: в маршруте он и есть строка.
    /// </summary>
    public string? Key { get; set; }

    /// <inheritdoc />
    protected override bool ReloadsOnAppearing => false;

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        _model.LoadAsync(Guid.TryParse(Key, out Guid key) ? key : null);

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Форма целиком не перечитывается — стёрла бы набранное, — а список
        // подкатегорий, поправленный с экрана поверх карточки, перечитывается
        if (_model.SubcategoriesOutdated)
        {
            Guarded.Run(() => _model.ReloadSubcategoriesAsync());
        }
    }

    private void OnSubcategoryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: CategoryRowItem { IsEditable: true } subcategory })
        {
            Navigator.Go($"{Routes.Subcategory}?key={subcategory.Key}");
        }
    }

    private void OnAddSubcategory(object? sender, EventArgs e)
    {
        if (_model.Key is { } group)
        {
            Navigator.Go($"{Routes.Subcategory}?group={group}");
        }
    }

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    private async Task SaveAsync()
    {
        if (await _model.SaveAsync())
        {
            await Navigator.GoAsync("..");
        }
    }
}
