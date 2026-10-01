using Finance.Application.Texts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Карточка счёта: заведение и правка одним экраном. Запреты показываются сразу —
/// валюта заперта операциями, дата открытия дальше первой операции не двигается, —
/// потому что узнать о них при сохранении поздно: пользователь уже всё ввёл.
/// </summary>
public sealed partial class AccountViewModel : ObservableObject, IFormModel
{
    private readonly IAccountCardQuery _query;
    private readonly ISaveAccountHandler _handler;
    private readonly IDeleteAccountHandler _delete;
    private readonly IClock _clock;

    // Как счёт записан: имя, заблокирован ли и сколько на нём. По ним видно, блокируют ли
    // счёт именно этой правкой и остаются ли на нём деньги, а удаление называет
    // счёт записанным именем, а не набранным в поле
    private string _savedName = string.Empty;
    private bool _savedClosed;
    private Money? _balance;

    // Снимок формы на момент загрузки: с ним сравнивается нынешнее состояние,
    // когда экран покидают, не сохранив. У новой формы снимок — её пустое начало
    private (string, AccountType, Currency, string, DateOnly, bool, bool) _saved;

    /// <summary>
    /// Создаёт модель представления карточки счёта.
    /// </summary>
    /// <param name="query">Чтение счёта для правки.</param>
    /// <param name="handler">Сохранение счёта.</param>
    /// <param name="delete">Удаление счёта.</param>
    /// <param name="clock">Часы: «сегодня» пользователя.</param>
    public AccountViewModel(IAccountCardQuery query, ISaveAccountHandler handler, IDeleteAccountHandler delete, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(clock);

        _query = query;
        _handler = handler;
        _delete = delete;
        _clock = clock;

        OpenedOn = clock.Today;
    }

    /// <summary>
    /// Ключ правимого счёта. Пусто — заводится новый.
    /// </summary>
    public Guid? Key { get; private set; }

    /// <summary>
    /// Наименование счёта.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// Наличные или карта.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NamePlaceholder))]
    public partial AccountType Type { get; set; } = AccountType.Card;

    /// <summary>
    /// Подсказка в пустом поле названия — своя у каждого типа: подсказка карты
    /// у наличных читалась бы так, будто форма не заметила смены типа. «Например»
    /// не даёт принять подсказку за уже введённое название.
    /// </summary>
    public string NamePlaceholder =>
        Type is AccountType.Cash ? UiTexts.AccountNamePlaceholderCash : UiTexts.AccountNamePlaceholderCard;

    /// <summary>
    /// Валюта счёта.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpeningBalanceDisplay))]
    public partial Currency Currency { get; set; } = Currency.RUB;

    private static readonly AccountType[] TypeOrder = [AccountType.Card, AccountType.Cash];

    private static readonly Currency[] CurrencyOrder = [Currency.RUB, Currency.USD, Currency.EUR];

    /// <summary>
    /// Подписи типов счёта для списка выбора.
    /// </summary>
    public IReadOnlyList<string> TypeNames => TypeCaptions;

    private static readonly string[] TypeCaptions = [UiTexts.AccountTypeCard, UiTexts.AccountTypeCash];

    /// <summary>
    /// Подписи валют для списка выбора.
    /// </summary>
    public IReadOnlyList<string> CurrencyNames => CurrencyCaptions;

    private static readonly string[] CurrencyCaptions =
        [UiTexts.CurrencyRubleOption, UiTexts.CurrencyDollarOption, UiTexts.CurrencyEuroOption];

    /// <summary>
    /// Выбранный тип счёта — номером в списке: список показывает подписи, а не имена членов.
    /// </summary>
    public int TypeIndex
    {
        get => Array.IndexOf(TypeOrder, Type);
        set => Type = TypeOrder[Math.Clamp(value, 0, TypeOrder.Length - 1)];
    }

    /// <summary>
    /// Выбранная валюта — номером в списке.
    /// </summary>
    public int CurrencyIndex
    {
        get => Array.IndexOf(CurrencyOrder, Currency);
        set => Currency = CurrencyOrder[Math.Clamp(value, 0, CurrencyOrder.Length - 1)];
    }

    /// <summary>
    /// Валюту менять можно: операций по счёту ещё не было.
    /// </summary>
    public bool CurrencyEditable => !CurrencyLocked;

    /// <summary>
    /// Дата открытия в том виде, в каком её принимает календарь.
    /// </summary>
    public DateTime OpenedOnDate
    {
        get => OpenedOn.ToDateTime(TimeOnly.MinValue);
        set => OpenedOn = DateOnly.FromDateTime(value);
    }

    /// <summary>
    /// Позднее сегодняшнего дня календарь не пускает: операций в будущем нет.
    /// А если по счёту уже есть операции — позднее первой из них: такую дату
    /// домен всё равно отверг бы, и лучше не предлагать её вовсе.
    /// </summary>
    public DateTime LatestOpeningDate =>
        (EarliestTransactionOn is { } earliest && earliest < _clock.Today ? earliest : _clock.Today)
            .ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Почему дату открытия дальше не сдвинуть. Показывается при открытии календаря:
    /// погашенные дни иначе выглядели бы ошибкой.
    /// </summary>
    public string? OpenedOnHint => EarliestTransactionOn is { } earliest
        ? string.Format(UiCulture.Current, UiTexts.AccountOpenedOnLimit, DateText.DayWithYearIfOther(earliest, _clock.Today))
        : null;

    /// <summary>
    /// Правило нарушено — сообщение показывается рядом с формой.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Начальный остаток, как он набран на клавиатуре суммы. Пусто — ноль: поле
    /// показывает его суммой с валютой, а минус с пустого поля начинает долг.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpeningBalanceDisplay))]
    [NotifyPropertyChangedFor(nameof(IsOpeningBalanceNegative))]
    public partial string OpeningBalance { get; set; } = string.Empty;

    /// <summary>
    /// Остаток для показа: число — суммой с валютой, выражение — как набрано.
    /// Итог выражения появляется по «=», а не сам собой, как в форме операции.
    /// </summary>
    public string OpeningBalanceDisplay
    {
        get
        {
            if (OpeningBalance.Length is 0)
            {
                return Money.Restore(0m, Currency).Display;
            }

            return !AmountInput.HasOperation(OpeningBalance) && AmountExpression.TryEvaluate(OpeningBalance, out decimal value)
                ? Money.Restore(value, Currency).Display
                : OpeningBalance;
        }
    }

    /// <summary>
    /// Остаток — долг: показывается цветом расхода.
    /// </summary>
    public bool IsOpeningBalanceNegative =>
        !AmountInput.HasOperation(OpeningBalance) && AmountExpression.TryEvaluate(OpeningBalance, out decimal value) && value < 0m;

    /// <summary>
    /// Клавиатура суммы на виду. Появляется по касанию остатка и уходит, пока набирают
    /// название: две клавиатуры на экран не помещаются, а остаток вводится раз на счёт.
    /// </summary>
    [ObservableProperty]
    public partial bool AreKeysVisible { get; set; }

    /// <summary>
    /// Дата открытия.
    /// </summary>
    [ObservableProperty]
    public partial DateOnly OpenedOn { get; set; }

    /// <summary>
    /// «Скрытый».
    /// </summary>
    [ObservableProperty]
    public partial bool ExcludedFromTotals { get; set; }

    /// <summary>
    /// «Счёт заблокирован».
    /// </summary>
    [ObservableProperty]
    public partial bool IsClosed { get; set; }

    /// <summary>
    /// Валюту менять нельзя: по счёту уже была операция.
    /// </summary>
    [ObservableProperty]
    public partial bool CurrencyLocked { get; private set; }

    /// <summary>
    /// Дальше этой даты открытие не сдвигается — раньше неё есть операция.
    /// </summary>
    [ObservableProperty]
    public partial DateOnly? EarliestTransactionOn { get; private set; }

    /// <summary>
    /// Текст нарушенного правила. Пусто — сохранять можно.
    /// </summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Идёт сохранение. Кнопка зовёт метод напрямую, минуя команду с её защитой
    /// от повторного запуска: без флага второе нажатие до ухода экрана
    /// завело бы второй счёт.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// Сохранять можно: предыдущее сохранение не идёт.
    /// </summary>
    public bool CanSave => !IsSaving;

    /// <inheritdoc />
    public bool IsDirty => Snapshot() != _saved;

    /// <summary>
    /// Правимые поля формы одним значением. Кортеж сравнивается сам, по всем
    /// полям сразу: список «что считать правкой» отдельно от полей разошёлся бы
    /// с ними при первом же новом поле.
    /// </summary>
    private (string Name, AccountType Type, Currency Currency, string OpeningBalance, DateOnly OpenedOn, bool Excluded, bool Closed) Snapshot() =>
        (Name, Type, Currency, OpeningBalance, OpenedOn, ExcludedFromTotals, IsClosed);

    /// <summary>
    /// Заголовок экрана.
    /// </summary>
    public string Title => Key is null ? UiTexts.AccountTitleNew : UiTexts.AccountTitleExisting;

    /// <summary>
    /// Правится записанный счёт — его можно удалить. У нового удалять нечего.
    /// </summary>
    public bool IsExisting => Key is not null;

    /// <summary>
    /// Почему счёт не удалить; пусто — удалять можно. Говорится в момент попытки,
    /// а не надписью на экране: удаляют редко, а блокировка — ответ на тот же вопрос.
    /// Решает домен при удалении; здесь — только чтобы не спрашивать подтверждения
    /// у того, что всё равно не удалится.
    /// </summary>
    public string? DeleteRefusal => EarliestTransactionOn is null ? null : UiTexts.AccountDeleteHasTransactions;

    /// <summary>
    /// Заголовок подтверждения удаления. Имя — записанное, а не набранное в поле:
    /// удаляется счёт как он сохранён.
    /// </summary>
    public string DeleteTitle => string.Format(UiCulture.Current, UiTexts.AccountDeleteConfirmTitle, _savedName);

    /// <summary>
    /// Предупреждение перед блокировкой счёта с деньгами; пусто — подтверждать нечего.
    /// Домен блокировке с остатком не мешает, но молча увести деньги из «доступно
    /// к тратам» нельзя: пользователь мог забыть перевести остаток.
    /// </summary>
    public string? ClosingWarning =>
        IsClosed && !_savedClosed && _balance is { Amount: not 0m } balance
            ? string.Format(UiCulture.Current, UiTexts.AccountClosingWarning, balance.Display)
            : null;

    /// <summary>
    /// Загружает счёт для правки. Пустой ключ оставляет форму пустой.
    /// </summary>
    /// <param name="key">Ключ счёта или <c>null</c> для нового.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(Guid? key, CancellationToken cancellationToken = default)
    {
        if (key is not { } existing)
        {
            // Новый счёт: правкой считается всё, что наберут поверх пустой формы
            _saved = Snapshot();

            return;
        }

        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        AccountCard? card = await _query.ReadAsync(existing, cancellationToken);

        if (card is null)
        {
            _saved = Snapshot();

            return;
        }

        Key = card.Key;
        Name = card.Name;
        Type = card.Type;
        Currency = card.Currency;
        // Тем же видом, каким набирает клавиатура: с запятой культуры, а не с точкой,
        // иначе стирание и дописывание сломали бы «1234.5»
        OpeningBalance = card.OpeningBalance is 0m ? string.Empty : AmountInput.Write(card.OpeningBalance);
        OpenedOn = card.OpenedOn;
        ExcludedFromTotals = card.ExcludedFromTotals;
        IsClosed = card.IsClosed;
        CurrencyLocked = card.CurrencyLocked;
        EarliestTransactionOn = card.EarliestTransactionOn;
        _savedName = card.Name;
        _savedClosed = card.IsClosed;
        _balance = card.Balance;
        _saved = Snapshot();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(TypeIndex));
        OnPropertyChanged(nameof(CurrencyIndex));
        OnPropertyChanged(nameof(CurrencyEditable));
        OnPropertyChanged(nameof(OpenedOnDate));
        OnPropertyChanged(nameof(OpenedOnHint));
        OnPropertyChanged(nameof(LatestOpeningDate));
    }

    /// <summary>
    /// Сохраняет счёт. Нарушенное доменное правило показывается текстом рядом
    /// с формой: это ввод пользователя, а не сбой, и падать приложению не за что.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если счёт сохранён и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
        {
            return false;
        }

        Error = null;
        OnPropertyChanged(nameof(HasError));

        decimal openingBalance = 0m;

        if (OpeningBalance.Length > 0 && !AmountExpression.TryEvaluate(OpeningBalance, out openingBalance))
        {
            Error = UiTexts.AccountOpeningIncomplete;
            OnPropertyChanged(nameof(HasError));

            return false;
        }

        IsSaving = true;
        bool done = false;

        try
        {
            await _handler
                .HandleAsync(
                    new SaveAccountCommand
                    {
                        Key = Key,
                        Name = Name,
                        Type = Type,
                        Currency = Currency,
                        OpeningBalance = openingBalance,
                        OpenedOn = OpenedOn,
                        ExcludedFromTotals = ExcludedFromTotals,
                        IsClosed = IsClosed
                    },
                    cancellationToken);

            done = true;

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;
            OnPropertyChanged(nameof(HasError));

            return false;
        }
        finally
        {
            // После удачи флаг остаётся: экран закрывается, и второе нажатие
            // в этот промежуток записало бы то же самое ещё раз. При неудаче
            // он снимается — нарушенное правило правят и сохраняют снова
            IsSaving = done;
        }
    }

    /// <summary>
    /// Удаляет счёт. Нарушенное правило — по счёту успели появиться операции —
    /// показывается текстом рядом с формой, как при сохранении.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если счёт удалён и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key || IsSaving)
        {
            return false;
        }

        Error = null;
        OnPropertyChanged(nameof(HasError));

        IsSaving = true;
        bool done = false;

        try
        {
            await _delete.HandleAsync(key, cancellationToken);

            done = true;

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;
            OnPropertyChanged(nameof(HasError));

            return false;
        }
        finally
        {
            // Как при сохранении: после удачи экран закрывается, и флаг держит
            // второе нажатие; после отказа снимается
            IsSaving = done;
        }
    }

    /// <summary>
    /// Нажата клавиша суммы: цифра, запятая или знак действия. Минус первой
    /// клавишей начинает отрицательный остаток — долг.
    /// </summary>
    /// <param name="key">Знак на клавише.</param>
    /// <exception cref="ArgumentException">Клавиша названа не одним знаком.</exception>
    [RelayCommand]
    public void PressKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (key.Length is not 1)
        {
            throw new ArgumentException(Faults.KeypadKeyIsOneSign(), nameof(key));
        }

        OpeningBalance = AmountInput.Append(OpeningBalance, key[0], signed: true);
    }

    /// <summary>
    /// Стирает последний набранный знак остатка.
    /// </summary>
    [RelayCommand]
    public void Backspace() => OpeningBalance = AmountInput.Backspace(OpeningBalance);

    /// <summary>
    /// Сворачивает набранное выражение в итог — клавиша «=».
    /// </summary>
    [RelayCommand]
    public void Evaluate() => OpeningBalance = AmountInput.Collapse(OpeningBalance);

    /// <summary>
    /// Показывает клавиатуру суммы — касание остатка.
    /// </summary>
    [RelayCommand]
    public void ShowKeys() => AreKeysVisible = true;
}
