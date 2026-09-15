using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Domain;

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
    private readonly IClock _clock;

    // Как счёт записан: закрыт ли и сколько на нём. По ним видно, закрывают ли
    // счёт именно этой правкой и остаются ли на нём деньги
    private bool _savedClosed;
    private Money? _balance;

    // Снимок формы на момент загрузки: с ним сравнивается нынешнее состояние,
    // когда экран покидают, не сохранив. У новой формы снимок — её пустое начало
    private (string, AccountType, Currency, string, DateOnly, bool, bool) _saved;

    /// <summary>Создаёт модель представления карточки счёта.</summary>
    /// <param name="query">Чтение счёта для правки.</param>
    /// <param name="handler">Сохранение счёта.</param>
    /// <param name="clock">Часы: «сегодня» пользователя.</param>
    public AccountViewModel(IAccountCardQuery query, ISaveAccountHandler handler, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(clock);

        _query = query;
        _handler = handler;
        _clock = clock;

        OpenedOn = clock.Today;
    }

    /// <summary>Ключ правимого счёта. Пусто — заводится новый.</summary>
    public Guid? Key { get; private set; }

    /// <summary>Наименование счёта.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Наличные или карта.</summary>
    [ObservableProperty]
    public partial AccountType Type { get; set; } = AccountType.Card;

    /// <summary>Валюта счёта.</summary>
    [ObservableProperty]
    public partial Currency Currency { get; set; } = Currency.RUB;

    private static readonly AccountType[] TypeOrder = [AccountType.Card, AccountType.Cash];

    private static readonly Currency[] CurrencyOrder = [Currency.RUB, Currency.USD, Currency.EUR];

    /// <summary>Подписи типов счёта для списка выбора.</summary>
    public IReadOnlyList<string> TypeNames => TypeCaptions;

    private static readonly string[] TypeCaptions = ["Карта", "Наличные"];

    /// <summary>Подписи валют для списка выбора.</summary>
    public IReadOnlyList<string> CurrencyNames => CurrencyCaptions;

    private static readonly string[] CurrencyCaptions = ["Рубль ₽", "Доллар $", "Евро €"];

    /// <summary>Выбранный тип счёта — номером в списке: список показывает подписи, а не имена членов.</summary>
    public int TypeIndex
    {
        get => Array.IndexOf(TypeOrder, Type);
        set => Type = TypeOrder[Math.Clamp(value, 0, TypeOrder.Length - 1)];
    }

    /// <summary>Выбранная валюта — номером в списке.</summary>
    public int CurrencyIndex
    {
        get => Array.IndexOf(CurrencyOrder, Currency);
        set => Currency = CurrencyOrder[Math.Clamp(value, 0, CurrencyOrder.Length - 1)];
    }

    /// <summary>Валюту менять можно: операций по счёту ещё не было.</summary>
    public bool CurrencyEditable => !CurrencyLocked;

    /// <summary>Дата открытия в том виде, в каком её принимает календарь.</summary>
    public DateTime OpenedOnDate
    {
        get => OpenedOn.ToDateTime(TimeOnly.MinValue);
        set => OpenedOn = DateOnly.FromDateTime(value);
    }

    /// <summary>Позднее сегодняшнего дня календарь не пускает: операций в будущем нет.</summary>
    public DateTime LatestOpeningDate => _clock.Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>Пояснение, почему дату открытия дальше не сдвинуть.</summary>
    public string? OpenedOnHint => EarliestTransactionOn is { } earliest
        ? $"Не позже {earliest:dd.MM.yyyy} — этим днём есть операция"
        : null;

    /// <summary>Пояснение к дате открытия есть — его стоит показать.</summary>
    public bool HasOpenedOnHint => OpenedOnHint is not null;

    /// <summary>Правило нарушено — сообщение показывается рядом с формой.</summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>Начальный остаток, как он набран в поле.</summary>
    [ObservableProperty]
    public partial string OpeningBalance { get; set; } = "0";

    /// <summary>Дата открытия.</summary>
    [ObservableProperty]
    public partial DateOnly OpenedOn { get; set; }

    /// <summary>«Скрыть из расчётов».</summary>
    [ObservableProperty]
    public partial bool ExcludedFromTotals { get; set; }

    /// <summary>«Счёт закрыт».</summary>
    [ObservableProperty]
    public partial bool IsClosed { get; set; }

    /// <summary>Валюту менять нельзя: по счёту уже была операция.</summary>
    [ObservableProperty]
    public partial bool CurrencyLocked { get; private set; }

    /// <summary>Дальше этой даты открытие не сдвигается — раньше неё есть операция.</summary>
    [ObservableProperty]
    public partial DateOnly? EarliestTransactionOn { get; private set; }

    /// <summary>Текст нарушенного правила. Пусто — сохранять можно.</summary>
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

    /// <summary>Сохранять можно: предыдущее сохранение не идёт.</summary>
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

    /// <summary>Заголовок экрана.</summary>
    public string Title => Key is null ? "Новый счёт" : "Счёт";

    /// <summary>
    /// Предупреждение перед закрытием счёта с деньгами; пусто — подтверждать нечего.
    /// Домен закрытию с остатком не мешает, но молча увести деньги из «доступно
    /// к тратам» нельзя: пользователь мог забыть перевести остаток.
    /// </summary>
    public string? ClosingWarning =>
        IsClosed && !_savedClosed && _balance is { Amount: not 0m } balance
            ? $"На счёте {balance.Display}. После закрытия они не войдут в «доступно к тратам», " +
              "а перевести их будет некуда — закрытый счёт в выборе не предлагается."
            : null;

    /// <summary>Загружает счёт для правки. Пустой ключ оставляет форму пустой.</summary>
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
        OpeningBalance = card.OpeningBalance.ToString(CultureInfo.InvariantCulture);
        OpenedOn = card.OpenedOn;
        ExcludedFromTotals = card.ExcludedFromTotals;
        IsClosed = card.IsClosed;
        CurrencyLocked = card.CurrencyLocked;
        EarliestTransactionOn = card.EarliestTransactionOn;
        _savedClosed = card.IsClosed;
        _balance = card.Balance;
        _saved = Snapshot();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TypeIndex));
        OnPropertyChanged(nameof(CurrencyIndex));
        OnPropertyChanged(nameof(CurrencyEditable));
        OnPropertyChanged(nameof(OpenedOnDate));
        OnPropertyChanged(nameof(OpenedOnHint));
        OnPropertyChanged(nameof(HasOpenedOnHint));
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

        if (!AmountExpression.TryEvaluate(OpeningBalance, out decimal openingBalance))
        {
            Error = "Начальный остаток введён не полностью";
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
            // в этот промежуток записало бы то же самое ещё раз
            IsSaving = !done;
        }
    }
}
