using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Domain;

namespace Finance.App.Pages;

/// <summary>Экраны B-01…B-06 и C-07: форма операции — запись, правка, удаление.</summary>
[QueryProperty(nameof(Key), "key")]
[QueryProperty(nameof(Account), "account")]
[QueryProperty(nameof(Kind), "kind")]
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

    /// <summary>Вид новой операции: им приходят с ярлыка на значке приложения.</summary>
    public string? Kind { get; set; }

    /// <inheritdoc />
    protected override bool ReloadsOnAppearing => false;

    /// <inheritdoc />
    protected override async Task LoadAsync()
    {
        await _model.LoadAsync(
            Guid.TryParse(Key, out Guid key) ? key : null,
            Guid.TryParse(Account, out Guid account) ? account : null,
            Enum.TryParse(Kind, out TransactionKind kind) ? kind : null);

        // Удалять нечего, пока операция не записана, поэтому пункт появляется,
        // а не гасится: на панели заголовка погашенный выглядит поломкой.
        // Ставится отсюда, а не убирается из разметки: убранный повторным
        // чтением было бы уже не вернуть
        if (_model.IsExisting && ToolbarItems.Count is 0)
        {
            ToolbarItems.Add(new ToolbarItem
            {
                Text = "Удалить",
                AutomationId = "DeleteTransaction",
                Command = new Command(() => Guarded.Run(DeleteAsync))
            });
        }
    }

    /// <summary>
    /// Третий чип даты открывает системный календарь. Сам контрол на экране
    /// невидим: у платформы нет другого способа показать её диалог выбора даты.
    /// </summary>
    private void OnPickDate(object? sender, TappedEventArgs e) => Calendar.IsOpen = true;

    /// <summary>
    /// Забирает выбор со своего экрана. Форма при возврате не перечитывается —
    /// набранная сумма должна пережить поход за счётом, — и это единственное место,
    /// где она узнаёт о выбранном.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _model.ApplyPicks();
    }

    /// <summary>
    /// Буквенная клавиатура системы и клавиши суммы на экран вместе не влезают,
    /// поэтому на время набора заметки клавиши уходят. Клавиша сохранения
    /// остаётся: снятие фокуса с поля зависит от системной клавиатуры, а закончить
    /// операцию пользователь должен мочь всегда.
    /// </summary>
    private void OnTextFocused(object? sender, FocusEventArgs e) => _model.AreKeysVisible = false;

    private void OnTextUnfocused(object? sender, FocusEventArgs e) => _model.AreKeysVisible = true;

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    /// <summary>
    /// Сохраняет и уходит с экрана. Нарушенное правило показывать нечем: карточка
    /// правила стоит над клавиатурой и попадает на экран сама.
    /// </summary>
    private async Task SaveAsync()
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
    private async Task DeleteAsync()
    {
        string consequence = await _model.DeletePromptAsync();

        bool confirmed = await DisplayAlertAsync("Удалить операцию?", consequence, "Удалить", "Отмена");

        if (confirmed && await _model.DeleteAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private void OnPickAccount(object? sender, TappedEventArgs e) =>
        Go($"{Routes.PickAccount}?selected={_model.SourceAccount?.Key}");

    /// <summary>
    /// Счёт списания в список «Куда» не попадает: перевод на себя запрещён доменом,
    /// и предлагать его значило бы рассказывать о запрете уже после сохранения.
    /// </summary>
    private void OnPickTargetAccount(object? sender, TappedEventArgs e) =>
        Go($"{Routes.PickAccount}?selected={_model.TargetAccount?.Key}&excluded={_model.SourceAccount?.Key}&target=1");

    private void OnPickCategory(object? sender, TappedEventArgs e) =>
        Go($"{Routes.PickCategory}?kind={_model.CategoryKind}&selected={_model.Category?.Key}");

    /// <summary>
    /// Название места уезжает в маршрут экранированным: в нём кириллица и пробелы,
    /// а неэкранированное оборвалось бы на первом же знаке разметки адреса.
    /// </summary>
    private void OnPickPlace(object? sender, TappedEventArgs e) =>
        Go($"{Routes.PickPlace}?current={Uri.EscapeDataString(_model.PlaceName)}");

    private static void Go(string route) => Guarded.Run(() => Shell.Current.GoToAsync(route));
}
