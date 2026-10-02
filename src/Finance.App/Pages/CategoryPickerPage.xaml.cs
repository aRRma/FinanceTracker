using Finance.App.Controls;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;

namespace Finance.App.Pages;

/// <summary>
/// Экран B-03: выбор подкатегории для формы операции. Шапка группы разворачивает
/// её, подкатегория выбирается и закрывает экран. Группа из одной подкатегории
/// выбирается сама, как подкатегория.
/// </summary>
[QueryProperty(nameof(Kind), "kind")]
[QueryProperty(nameof(Selected), "selected")]
public sealed partial class CategoryPickerPage : DataPage
{
    private readonly CategoryPickerViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления выбора подкатегории.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public CategoryPickerPage(CategoryPickerViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Вид категорий: он задан видом операции.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Подкатегория, стоящая в форме сейчас.
    /// </summary>
    public string? Selected { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync(
        Enum.TryParse(Kind, out CategoryKind kind) ? kind : CategoryKind.Expense,
        Guid.TryParse(Selected, out Guid selected) ? selected : null);

    private void OnLineTapped(object? sender, TappedEventArgs e)
    {
        // Подпись раздела не выбирается: касание по ней не должно закрыть экран
        if (sender is not BindableObject { BindingContext: CategoryPickerLine { IsSection: false } line })
        {
            return;
        }

        if (line.IsExpandable)
        {
            // Разворачивает вся шапка, а не один знак: выбирать здесь нечего,
            // и вторая зона касания только мешала бы
            Guarded.Run(() => Chevron.TurnAsync(sender, line.IsExpanded, () => _model.Toggle(line)));

            return;
        }

        _model.Pick(line);

        Navigator.Go("..");
    }
}
