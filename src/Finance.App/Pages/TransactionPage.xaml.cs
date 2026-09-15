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
    protected override bool ReloadsOnAppearing => false;

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
    /// Буквенная клавиатура системы и клавиши суммы на экран вместе не влезают,
    /// поэтому на время набора места и заметки клавиши уходят. Клавиша сохранения
    /// остаётся: снятие фокуса с поля зависит от системной клавиатуры, а закончить
    /// операцию пользователь должен мочь всегда.
    /// </summary>
    private void OnTextFocused(object? sender, FocusEventArgs e) => _model.AreKeysVisible = false;

    private void OnTextUnfocused(object? sender, FocusEventArgs e) => _model.AreKeysVisible = true;

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    private async Task SaveAsync()
    {
        if (await _model.SaveAsync())
        {
            await Shell.Current.GoToAsync("..");

            return;
        }

        // Нарушенное правило стоит последним в форме и с нижней части экрана не
        // видно: без подвода к нему нажатие выглядит так, будто ничего не произошло
        if (_model.HasError)
        {
            ScrollToError();
        }
    }

    /// <summary>
    /// Подводит форму к нарушенному правилу. Карточка правила до этого была скрыта,
    /// и до конца разметки её положение ещё нулевое — прокрутка по нему увела бы
    /// экран в начало формы. Поэтому уже измеренную двигаем сразу, а только что
    /// показанную — по первому же измерению.
    /// </summary>
    private void ScrollToError()
    {
        if (ErrorCard.Height > 0)
        {
            _ = Form.ScrollToAsync(ErrorCard, ScrollToPosition.End, animated: true);

            return;
        }

        ErrorCard.SizeChanged -= OnErrorMeasured;
        ErrorCard.SizeChanged += OnErrorMeasured;
    }

    private void OnErrorMeasured(object? sender, EventArgs e)
    {
        ErrorCard.SizeChanged -= OnErrorMeasured;

        _ = Form.ScrollToAsync(ErrorCard, ScrollToPosition.End, animated: true);
    }

    private void OnDelete(object? sender, EventArgs e) => Guarded.Run(DeleteAsync);

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

    private void OnPlacePicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: PlaceOption place })
        {
            _model.PickPlace(place);
        }
    }
}
