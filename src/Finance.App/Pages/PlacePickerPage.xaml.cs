using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран B-07: выбор места для формы операции. Выбранное, новое или снятое место
/// кладётся в общий объект, и экран закрывается.
/// </summary>
[QueryProperty(nameof(Current), "current")]
public sealed partial class PlacePickerPage : DataPage
{
    private readonly PlacePickerViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления выбора места.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public PlacePickerPage(PlacePickerViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Место, стоящее в форме сейчас.
    /// </summary>
    public string? Current { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync(Uri.UnescapeDataString(Current ?? string.Empty));

    private void OnPlaceTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: PlacePickerRow row })
        {
            _model.Pick(row);

            Leave();
        }
    }

    private void OnCreate(object? sender, TappedEventArgs e)
    {
        _model.Create();

        Leave();
    }

    private void OnClear(object? sender, TappedEventArgs e)
    {
        _model.ClearPlace();

        Leave();
    }

    private static void Leave() => Navigator.Go("..");
}
