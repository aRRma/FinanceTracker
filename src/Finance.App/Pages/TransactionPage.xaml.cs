using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экраны B-01…B-06 и C-07: форма операции — запись, правка, удаление.</summary>
[QueryProperty(nameof(Key), "key")]
[QueryProperty(nameof(Account), "account")]
public partial class TransactionPage : DataPage
{
    private readonly TransactionViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления формы операции.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public TransactionPage(TransactionViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>Ключ правимой операции из маршрута. Пусто — записывается новая.</summary>
    public string? Key { get; set; }

    /// <summary>Счёт для подстановки в новую операцию — с чьей ленты пришли.</summary>
    public string? Account { get; set; }

    /// <inheritdoc />
    protected override async Task LoadAsync()
    {
        await _model.LoadAsync(
            Guid.TryParse(Key, out Guid key) ? key : null,
            Guid.TryParse(Account, out Guid account) ? account : null);

        // Удалять нечего, пока операция не записана: кнопка убирается,
        // а не гасится — на панели заголовка погашенная выглядит поломкой
        if (!_model.IsExisting)
        {
            ToolbarItems.Clear();
        }
    }

    /// <summary>
    /// Буквенная клавиатура системы и клавиатура суммы на экран вместе не влезают,
    /// поэтому на время набора места и заметки вторая уходит.
    /// </summary>
    private void OnTextFocused(object? sender, FocusEventArgs e) => _model.IsKeypadVisible = false;

    private void OnTextUnfocused(object? sender, FocusEventArgs e) => _model.IsKeypadVisible = true;

    private async void OnSave(object? sender, EventArgs e)
    {
        if (await _model.SaveAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    /// <summary>
    /// Подтверждение обязательно: отмены и корзины нет, и диалог — единственная
    /// защита. Он называет последствие — каким станет баланс.
    /// </summary>
    private async void OnDelete(object? sender, EventArgs e)
    {
        string consequence = await _model.DeletePromptAsync();

        bool confirmed = await DisplayAlertAsync("Удалить операцию?", consequence, "Удалить", "Отмена");

        if (confirmed && await _model.DeleteAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private void OnPlacePicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: PlaceOption place })
        {
            _model.PickPlace(place);
        }
    }
}
