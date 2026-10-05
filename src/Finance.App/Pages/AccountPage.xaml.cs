using Finance.Application.Texts;
using Finance.Application.Features.Accounts.Card;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-02: карточка счёта — заведение и правка.
/// </summary>
[QueryProperty(nameof(Key), "key")]
public sealed partial class AccountPage : DataPage
{
    private readonly AccountViewModel _model;

    private bool _deleting;

    // Клавиатура суммы только что позвана: ужатую ею форму докрутить до остатка
    private bool _scrollToOpening;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
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
    protected override bool ReloadsOnAppearing => false;

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        _model.LoadAsync(Guid.TryParse(Key, out Guid key) ? key : null);

    /// <summary>
    /// Вернувшись с экрана «Цвет и значок», карточка забирает выбор: перечитывать
    /// форму при появлении нельзя — стёрлось бы набранное.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.ApplyBadge();
    }

    private void OnSave(object? sender, EventArgs e) => Guarded.Run(SaveAsync);

    private void OnBadgeTapped(object? sender, TappedEventArgs e)
    {
        _model.AreKeysVisible = false;
        _model.OpenBadge();
        Navigator.Go(Routes.AccountBadge);
    }

    private void OnDelete(object? sender, EventArgs e)
    {
        // Второе касание до ответа на первый вопрос открыло бы второй диалог
        if (_deleting)
        {
            return;
        }

        _deleting = true;
        Guarded.Run(async () =>
        {
            try
            {
                await DeleteAsync();
            }
            finally
            {
                _deleting = false;
            }
        });
    }

    // Две клавиатуры на экран не помещаются: набор названия убирает клавиатуру суммы
    private void OnNameFocused(object? sender, FocusEventArgs e) => _model.AreKeysVisible = false;

    private void OnOpeningBalanceTapped(object? sender, TappedEventArgs e) => Guarded.Run(ShowKeysAsync);

    /// <summary>
    /// Клавиатура суммы поднялась и ужала форму — остаток докручивается над ней:
    /// на тесном экране он уходил под клавиши, и набирали вслепую.
    /// </summary>
    /// <remarks>
    /// Один раз на появление клавиатуры: форма меняет размер и от карточки правила,
    /// и докрутка на каждую смену уводила бы от места, куда пользователь долистал сам.
    /// </remarks>
    private void OnFormSizeChanged(object? sender, EventArgs e)
    {
        if (_scrollToOpening && _model.AreKeysVisible)
        {
            _scrollToOpening = false;
            Guarded.Run(() => Form.ScrollToAsync(OpeningField, ScrollToPosition.MakeVisible, animated: true));
        }
    }

    /// <summary>
    /// «Назад» сначала убирает клавиатуру суммы, как системную клавиатуру Android,
    /// и только потом уводит с экрана.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (_model.AreKeysVisible)
        {
            _model.AreKeysVisible = false;

            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Касание остатка зовёт клавиатуру суммы, а системную, если она осталась
    /// от названия, убирает явно: снятый фокус на Android её не прячет.
    /// </summary>
    private async Task ShowKeysAsync()
    {
        if (NameEntry.IsSoftInputShowing())
        {
            await NameEntry.HideSoftInputAsync(CancellationToken.None);
        }

        NameEntry.Unfocus();

        // Форму ужмёт только появившаяся клавиатура: уже показанная размер не меняет
        _scrollToOpening = !_model.AreKeysVisible;
        _model.ShowKeys();
    }

    /// <summary>
    /// Погашенное поле валюты само ничего не объясняет, поэтому касание по нему
    /// говорит, почему валюту не сменить.
    /// </summary>
    private void OnLockedCurrencyTapped(object? sender, TappedEventArgs e) => Notice.Show(UiTexts.AccountCurrencyLocked);

    /// <summary>
    /// Календарь гасит дни после первой операции по счёту. Сообщение объясняет
    /// это в ту минуту, когда погашенные дни видны.
    /// </summary>
    private void OnOpenedOnPickerOpened(object? sender, DatePickerOpenedEventArgs e)
    {
        if (_model.OpenedOnHint is { } hint)
        {
            Notice.Show(hint);
        }
    }

    /// <summary>
    /// Блокировка счёта с деньгами подтверждается отдельно: домен её не запрещает,
    /// а остаток молча уходит из «доступно к тратам».
    /// </summary>
    private async Task SaveAsync()
    {
        if (_model.ClosingWarning is { } warning
            && !await DisplayAlertAsync(UiTexts.AccountCloseConfirmTitle, warning, UiTexts.AccountCloseConfirm, UiTexts.CommonCancel))
        {
            return;
        }

        if (await _model.SaveAsync())
        {
            await Navigator.GoAsync("..");
        }
    }

    /// <summary>
    /// Счёт с операциями не удаляется — об этом говорится сразу, без вопроса,
    /// на который всё равно пришёл бы отказ. Пустой удаляется после подтверждения.
    /// </summary>
    private async Task DeleteAsync()
    {
        if (_model.DeleteRefusal is { } refusal)
        {
            Notice.Show(refusal);

            return;
        }

        if (!await DisplayAlertAsync(_model.DeleteTitle, null, UiTexts.CommonDelete, UiTexts.CommonCancel))
        {
            return;
        }

        if (await _model.DeleteAsync())
        {
            // Карточку открыли карандашом из ленты этого счёта — «назад» вернул бы
            // в ленту удалённого счёта, поэтому уходим на шаг дальше, туда, откуда
            // открыли ленту
            IReadOnlyList<Page> stack = Navigation.NavigationStack;
            bool fromFeed = stack.Count > 1 && stack[^2] is AccountFeedPage;

            await Navigator.GoAsync(fromFeed ? "../.." : "..");
        }
    }
}
