using Finance.App.Controls;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-03: справочник категорий.
/// </summary>
public sealed partial class CategoriesPage : DataPage
{
    private readonly CategoriesViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
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

    private void OnCreateGroup(object? sender, EventArgs e) =>
        Navigator.Go(Routes.Group);

    /// <summary>
    /// Открывает карточку. «Прочее» и служебные не открываются: переносить
    /// и удалять их нельзя, а карточка без единого доступного действия сбивает
    /// с толку. Разворот группы живёт на шевроне — у него своя зона касания.
    /// </summary>
    private void OnLineTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: CategoryLine { IsEditable: true } line })
        {
            return;
        }

        string route = line.IsGroup ? Routes.Group : Routes.Subcategory;

        Navigator.Go($"{route}?key={line.Key}");
    }

    private void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: CategoryLine line })
        {
            Guarded.Run(() => Chevron.TurnAsync(sender, line.IsExpanded, () => _model.Toggle(line)));
        }
    }
}
