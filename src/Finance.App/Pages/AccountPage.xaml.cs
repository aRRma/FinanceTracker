using Finance.Application.Features.Accounts.Card;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран D-02: карточка счёта — заведение и правка.</summary>
[QueryProperty(nameof(Key), "key")]
public partial class AccountPage : DataPage
{
    private readonly AccountViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления карточки счёта.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AccountPage(AccountViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Ключ правимого счёта из маршрута. Пусто — заводится новый счёт.
    /// Строкой, а не <see cref="Guid"/>: в маршруте он и есть строка.
    /// </summary>
    public string? Key { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        _model.LoadAsync(Guid.TryParse(Key, out Guid key) ? key : null);

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    private async Task SaveAsync()
    {
        if (await _model.SaveAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
