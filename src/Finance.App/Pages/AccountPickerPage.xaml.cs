using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран B-02: выбор счёта для формы операции. Выбор кладётся в общий объект,
/// и экран закрывается сам: форма забирает решение, когда снова появится.
/// </summary>
[QueryProperty(nameof(Selected), "selected")]
[QueryProperty(nameof(Excluded), "excluded")]
[QueryProperty(nameof(Target), "target")]
public partial class AccountPickerPage : DataPage
{
    private readonly AccountPickerViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления выбора счёта.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AccountPickerPage(AccountPickerViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>Счёт, стоящий в форме сейчас.</summary>
    public string? Selected { get; set; }

    /// <summary>Счёт, которого в списке быть не должно: списание при выборе «Куда».</summary>
    public string? Excluded { get; set; }

    /// <summary>Непусто — выбирается счёт зачисления перевода.</summary>
    public string? Target { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync(
        Guid.TryParse(Selected, out Guid selected) ? selected : null,
        Guid.TryParse(Excluded, out Guid excluded) ? excluded : null,
        !string.IsNullOrEmpty(Target));

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: AccountPickerRow row })
        {
            _model.Pick(row);

            Guarded.Run(() => Shell.Current.GoToAsync(".."));
        }
    }
}
