using Finance.Application.Texts;
using Finance.Application.Features.Settings.TimeZones;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-08: выбор часового пояса.
/// </summary>
public sealed partial class TimeZonePage : DataPage
{
    private readonly TimeZoneViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана часового пояса.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public TimeZonePage(TimeZoneViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private void OnZoneTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: TimeZoneOption option })
        {
            Guarded.Run(() => SelectAsync(option));
        }
    }

    private async Task SelectAsync(TimeZoneOption option)
    {
        try
        {
            await _model.SelectAsync(option);
        }
        catch (TimeZoneNotFoundException error)
        {
            // Зона пришла из системного списка и пропасть из него не может,
            // но список читается один раз на заход: за это время систему могли
            // обновить, и молчаливый отказ выглядел бы как несработавшее касание
            await DisplayAlertAsync(UiTexts.TimeZoneUnavailableTitle, error.Message, UiTexts.CommonClose);
        }
    }
}
