using Finance.Application.Features.Places.Card;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран C-08: карточка места — переименование и удаление.</summary>
[QueryProperty(nameof(Key), "key")]
public partial class PlacePage : DataPage
{
    private readonly PlaceViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления карточки места.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public PlacePage(PlaceViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>Ключ правимого места из маршрута.</summary>
    public string? Key { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        Guid.TryParse(Key, out Guid key) ? _model.LoadAsync(key) : Task.CompletedTask;

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    private async Task SaveAsync()
    {
        if (await _model.SaveAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private void OnDelete(object? sender, EventArgs e) => Guarded.Run(DeleteAsync);

    // Подтверждение называет число операций, которые останутся без места:
    // отменить удаление нельзя, а по числу видно, то ли это место
    private async Task DeleteAsync()
    {
        if (!await DisplayAlertAsync(_model.DeleteTitle, _model.DeletePrompt, "Удалить", "Отмена"))
        {
            return;
        }

        if (await _model.DeleteAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
