using Finance.Application.Texts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Features.Accounts.Badge;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Карточка счёта: заведение, правка и удаление одним экраном. Запреты показываются
/// сразу — валюта заперта операциями, дата открытия дальше первой операции не двигается,
/// счёт с операциями не удаляется, — потому что узнать о них при сохранении поздно.
/// Остаток набирается клавиатурой суммы, как сумма операции, а долг — минусом первым.
/// </summary>
public sealed partial class AccountViewModel : FormViewModel
{
    private readonly IAccountCardQuery _query;
    private readonly ISaveAccountHandler _handler;
    private readonly IDeleteAccountHandler _delete;
    private readonly IClock _clock;
    private readonly AccountBadgeDraft _badge;

    // Как счёт записан: имя, заблокирован ли и сколько на нём. По ним видно, блокируют ли
    // счёт именно этой правкой и остаются ли на нём деньги, а удаление называет
    // счёт записанным именем, а не набранным в поле
    private string _savedName = string.Empty;
    private bool _savedClosed;
    private Money? _balance;

    // Снимок формы на момент загрузки: с ним сравнивается нынешнее состояние,
    // когда экран покидают, не сохранив. У новой формы снимок — её пустое начало
    private (string Name, AccountType Type, AccountColor Color, string? Icon, Currency Currency, decimal? Opening, string OpeningTyped, DateOnly OpenedOn, bool Excluded, bool Closed) _saved;

    /// <summary>
    /// Создаёт модель представления карточки счёта.
    /// </summary>
    /// <param name="query">Чтение счёта для правки.</param>
    /// <param name="handler">Сохранение счёта.</param>
    /// <param name="delete">Удаление счёта.</param>
    /// <param name="clock">Часы: «сегодня» пользователя.</param>
    /// <param name="badge">Цвет и значок по дороге на экран выбора и обратно.</param>
    public AccountViewModel(
        IAccountCardQuery query,
        ISaveAccountHandler handler,
        IDeleteAccountHandler delete,
        IClock clock,
        AccountBadgeDraft badge)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(badge);

        _query = query;
        _handler = handler;
        _delete = delete;
        _clock = clock;
        _badge = badge;

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
    [NotifyPropertyChangedFor(nameof(Mark))]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// Наличные или карта.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NamePlaceholder))]
    [NotifyPropertyChangedFor(nameof(Mark))]
    [NotifyPropertyChangedFor(nameof(BadgeCaption))]
    [NotifyPropertyChangedFor(nameof(HeaderCaption))]
    public partial AccountType Type { get; set; } = AccountType.Card;

    /// <summary>
    /// Цвет счёта. Новому подбирается по очереди ещё до набора — карточка сразу
    /// показывает счёт таким, каким он встанет на балансы.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mark))]
    [NotifyPropertyChangedFor(nameof(BadgeCaption))]
    public partial AccountColor Color { get; private set; } = AccountColor.Blue;

    /// <summary>
    /// Значок, выбранный руками; пусто — по типу, и тогда он следует за сменой типа.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mark))]
    [NotifyPropertyChangedFor(nameof(BadgeCaption))]
    public partial string? Icon { get; private set; }

    /// <summary>
    /// Знак счёта в шапке карточки и в строке «Цвет и значок».
    /// </summary>
    public AccountMark Mark => new(Name, Color, AccountIcon.For(Type, ExcludedFromTotals, Icon));

    /// <summary>
    /// Строка «Цвет и значок» словами: «Голубой · процент».
    /// </summary>
    public string BadgeCaption => AccountBadgeText.Describe(Color, Mark.Icon);

    /// <summary>
    /// Подпись под названием в шапке: тип и валюта, как в справочнике счетов.
    /// </summary>
    public string HeaderCaption =>
        $"{(Type is AccountType.Cash ? UiTexts.AccountTypeCash : UiTexts.AccountTypeCard)} · {Currency}";

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
    [NotifyPropertyChangedFor(nameof(HeaderCaption))]
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
    /// Начальный остаток, как он набран на клавиатуре суммы. Пусто — ноль: поле
    /// показывает его суммой с валютой, а минус с пустого поля начинает долг.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpeningBalanceDisplay))]
    [NotifyPropertyChangedFor(nameof(IsOpeningBalanceNegative))]
    public partial string OpeningBalance { get; set; } = string.Empty;

    /// <summary>
    /// Остаток для показа. Пока клавиатура на виду — как набран: сумма с валютой
    /// прятала бы запятую и нули после неё, и нажатие выглядело бы непринятым.
    /// Без клавиатуры число — суммой с валютой, дробная часть — только если набрана
    /// запятая; незакрытое выражение — как набрано:
    /// итог выражения появляется по «=», а не сам собой, как в форме операции.
    /// </summary>
    public string OpeningBalanceDisplay =>
        (AreKeysVisible && OpeningBalance.Length > 0) || AmountInput.HasOperation(OpeningBalance) || OpeningValue is not { } value
            ? OpeningBalance
            : Money.Restore(value, Currency).DisplayTyped(Settled);

    /// <summary>
    /// Набранное без висящей запятой. Суммой остаток показан, только когда набор
    /// окончен, а тогда «1500,» — то же 1500: «,00» обещало бы копейки, которых после
    /// сохранения и повторного открытия уже не будет.
    /// </summary>
    private string Settled => OpeningBalance.TrimEnd(AmountInput.Separator);

    /// <summary>
    /// Остаток — долг: показывается цветом расхода.
    /// </summary>
    public bool IsOpeningBalanceNegative => !AmountInput.HasOperation(OpeningBalance) && OpeningValue < 0m;

    /// <summary>
    /// Клавиатура суммы на виду. Появляется по касанию остатка и уходит, пока набирают
    /// название: две клавиатуры на экран не помещаются, а остаток вводится раз на счёт.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpeningBalanceDisplay))]
    public partial bool AreKeysVisible { get; set; }

    /// <summary>
    /// Набранный остаток числом: пусто — ноль, выражение — его итог. Не считается —
    /// пусто: набор не закончен («−», «100+»).
    /// </summary>
    private decimal? OpeningValue => OpeningBalance.Length is 0
        ? 0m
        : AmountExpression.TryEvaluate(OpeningBalance, out decimal value) ? value : null;

    /// <summary>
    /// Дата открытия.
    /// </summary>
    [ObservableProperty]
    public partial DateOnly OpenedOn { get; set; }

    /// <summary>
    /// «Скрытый».
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mark))]
    [NotifyPropertyChangedFor(nameof(BadgeCaption))]
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

    /// <inheritdoc />
    public override bool IsDirty => Snapshot() != _saved;

    /// <summary>
    /// Правимые поля формы одним значением. Кортеж сравнивается сам, по всем
    /// полям сразу: список «что считать правкой» отдельно от полей разошёлся бы
    /// с ними при первом же новом поле. Остаток — числом, а не набранной строкой:
    /// «0» на пустом поле и «1234,50» вместо «1234,5» ничего не меняют. Строка
    /// идёт в снимок, только пока набор не считается.
    /// </summary>
    private (string Name, AccountType Type, AccountColor Color, string? Icon, Currency Currency, decimal? Opening, string OpeningTyped, DateOnly OpenedOn, bool Excluded, bool Closed) Snapshot() =>
        (Name, Type, Color, Icon, Currency, OpeningValue, OpeningValue is null ? OpeningBalance : string.Empty, OpenedOn, ExcludedFromTotals, IsClosed);

    /// <summary>
    /// Заголовок экрана.
    /// </summary>
    public string Title => Key is null ? UiTexts.AccountTitleNew : UiTexts.AccountTitleExisting;

    /// <summary>
    /// Правится записанный счёт — его можно удалить и заблокировать. У нового удалять
    /// нечего, а блокировать незачем.
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
    public async Task LoadAsync(Guid? key, CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        AccountCard? card = key is { } existing ? await _query.ReadAsync(existing, cancellationToken) : null;

        if (card is null)
        {
            // Новый счёт: снимок — пустая форма, снятая до чтения цвета, иначе набранное
            // за время чтения вошло бы в снимок и правкой не считалось. Подобранный цвет
            // ставится и в снимок: подставленное формой правкой не считается, и нетронутая
            // карточка не спросит о несохранённом
            _saved = Snapshot();

            AccountColor color = await _query.ReadFreeColorAsync(cancellationToken);
            Color = color;
            _saved.Color = color;

            return;
        }

        Key = card.Key;
        Name = card.Name;
        Type = card.Type;
        Color = card.Color;
        Icon = card.Icon;
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
    /// Сохраняет счёт.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если счёт сохранён и экран можно закрыть.</returns>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
        {
            return false;
        }

        if (OpeningValue is not { } openingBalance)
        {
            Error = UiTexts.AccountOpeningIncomplete;

            return false;
        }

        return await WriteAsync(
            token => _handler.HandleAsync(
                new SaveAccountCommand
                {
                    Key = Key,
                    Name = Name,
                    Type = Type,
                    Color = Color,
                    Icon = Icon,
                    Currency = Currency,
                    OpeningBalance = openingBalance,
                    OpenedOn = OpenedOn,
                    ExcludedFromTotals = ExcludedFromTotals,
                    IsClosed = IsClosed
                },
                token),
            cancellationToken);
    }

    /// <summary>
    /// Удаляет счёт. Нарушенное правило — по счёту успели появиться операции —
    /// показывается текстом рядом с формой, как при сохранении.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если счёт удалён и экран можно закрыть.</returns>
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default) =>
        Key is { } key && await WriteAsync(token => _delete.HandleAsync(key, token), cancellationToken);

    /// <summary>
    /// Нажата клавиша суммы: цифра, запятая или знак действия. Минус первой
    /// клавишей начинает отрицательный остаток — долг.
    /// </summary>
    /// <param name="key">Знак на клавише.</param>
    /// <exception cref="ArgumentOutOfRangeException">Клавиши с таким знаком на клавиатуре нет.</exception>
    [RelayCommand]
    public void PressKey(char key) => OpeningBalance = AmountInput.Append(OpeningBalance, key, signed: true);

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
    public void ShowKeys() => AreKeysVisible = true;

    /// <summary>
    /// Карточка уходит на экран «Цвет и значок»: кладёт счёт, как он набран сейчас, —
    /// предпросмотр там показывает набранное название, а не записанное.
    /// </summary>
    public void OpenBadge()
    {
        // Баланс — от набранного остатка: правка остатка сдвигает баланс на разницу,
        // и предпросмотр показывает тот, что встанет на балансы после сохранения.
        // Недобранный остаток («100+») не считается — остаётся записанный
        decimal movement = _balance is { } saved ? saved.Amount - (_saved.Opening ?? 0m) : 0m;
        Money balance = Money.Restore((OpeningValue ?? _saved.Opening ?? 0m) + movement, Currency);

        _badge.Start(Key, Name, Type, ExcludedFromTotals, balance.Display, balance.IsNegative, Color, Icon);
    }

    /// <summary>
    /// Карточка вернулась на экран: забирает выбор, если на экране выбора что-то меняли.
    /// </summary>
    public void ApplyBadge()
    {
        if (_badge.TryTake(out AccountColor color, out string? icon))
        {
            Color = color;
            Icon = icon;
        }
    }
}
