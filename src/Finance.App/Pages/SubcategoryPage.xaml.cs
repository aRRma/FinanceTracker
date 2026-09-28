using Finance.Application.Texts;
using Finance.Application.Features.Categories.Card;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран C-02: карточка подкатегории — заведение, правка, перенос и удаление.
/// </summary>
[QueryProperty(nameof(Key), "key")]
[QueryProperty(nameof(Group), "group")]
public partial class SubcategoryPage : DataPage
{
    private readonly SubcategoryViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления карточки подкатегории.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public SubcategoryPage(SubcategoryViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Ключ правимой подкатегории из маршрута. Пусто — заводится новая.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Группа новой подкатегории из маршрута.
    /// </summary>
    public string? Group { get; set; }

    /// <inheritdoc />
    protected override bool ReloadsOnAppearing => false;

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        _model.LoadAsync(
            Guid.TryParse(Key, out Guid key) ? key : null,
            Guid.TryParse(Group, out Guid group) ? group : null);

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    private async Task SaveAsync()
    {
        if (await _model.SaveAsync())
        {
            await Navigator.GoAsync("..");
        }
    }

    private void OnDelete(object? sender, EventArgs e) => Guarded.Run(DeleteAsync);

    // Подтверждение называет число операций и приёмник: переезд необратим,
    // и подтверждать его вслепую нельзя
    private async Task DeleteAsync()
    {
        string prompt = await _model.DeletePromptAsync();

        if (!await DisplayAlertAsync(
            string.Format(UiCulture.Current, UiTexts.SubcategoryDeleteConfirmTitle, _model.Name),
            prompt,
            UiTexts.CommonDelete,
            UiTexts.CommonCancel))
        {
            return;
        }

        if (await _model.DeleteAsync())
        {
            await Navigator.GoAsync("..");
        }
    }
}
