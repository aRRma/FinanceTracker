using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;

namespace Finance.App.Pages;

/// <summary>
/// Экран B-02: выбор счёта для формы операции. Выбор кладётся в общий объект,
/// и экран закрывается сам: форма забирает решение, когда снова появится.
/// </summary>
[QueryProperty(nameof(Selected), "selected")]
[QueryProperty(nameof(Excluded), "excluded")]
[QueryProperty(nameof(Target), "target")]
[QueryProperty(nameof(Kind), "kind")]
public partial class AccountPickerPage : DataPage
{
    private readonly AccountPickerViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
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

    /// <summary>
    /// Счёт, стоящий в форме сейчас.
    /// </summary>
    public string? Selected { get; set; }

    /// <summary>
    /// Счёт, которого в списке быть не должно: списание при выборе «Куда».
    /// </summary>
    public string? Excluded { get; set; }

    /// <summary>
    /// Непусто — выбирается счёт зачисления перевода.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Вид операции в форме: от него зависит заголовок.
    /// </summary>
    public string? Kind { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync(
        Enum.TryParse(Kind, out TransactionKind kind) ? kind : TransactionKind.Expense,
        Guid.TryParse(Selected, out Guid selected) ? selected : null,
        Guid.TryParse(Excluded, out Guid excluded) ? excluded : null,
        !string.IsNullOrEmpty(Target));

    /// <summary>
    /// Новый счёт заводится вместо этого экрана, а не поверх него: «..» снимает выбор,
    /// и после сохранения карточка вернётся прямо в форму. Форма уже ждёт счёт —
    /// попросила, открывая выбор, — и сама поставит заведённый в своё поле.
    /// </summary>
    private void OnNewAccount(object? sender, TappedEventArgs e) => Navigator.Go($"../{Routes.Account}");

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: AccountPickerRow row })
        {
            _model.Pick(row);

            Navigator.Go("..");
        }
    }
}
