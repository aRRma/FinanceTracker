using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Форма операции: запись и правка одним экраном. Самый частый сценарий приложения,
/// поэтому всё, что можно подставить, подставлено — последний счёт и сегодняшняя
/// дата, — а категория выбирается каждый раз: подставленная не глядя категория
/// портит отчёт молча.
/// </summary>
public sealed partial class TransactionViewModel : ObservableObject
{
    private const int SuggestionLimit = 8;

    private static readonly TransactionKind[] KindOrder =
        [TransactionKind.Expense, TransactionKind.Income, TransactionKind.Transfer];

    private readonly ITransactionFormQuery _form;
    private readonly ITransactionCardQuery _card;
    private readonly ISaveTransactionHandler _save;
    private readonly IDeleteTransactionHandler _delete;
    private readonly IAccountsQuery _accounts;
    private readonly IClock _clock;

    private IReadOnlyList<AccountOption> _allAccounts = [];
    private IReadOnlyList<CategoryOption> _allCategories = [];
    private IReadOnlyList<PlaceOption> _allPlaces = [];

    /// <summary>Создаёт модель представления формы операции.</summary>
    /// <param name="form">Списки выбора формы.</param>
    /// <param name="card">Чтение операции для правки.</param>
    /// <param name="save">Запись и правка операции.</param>
    /// <param name="delete">Удаление операции.</param>
    /// <param name="accounts">Балансы счетов — для текста подтверждения удаления.</param>
    /// <param name="clock">Часы: «сегодня» пользователя.</param>
    public TransactionViewModel(
        ITransactionFormQuery form,
        ITransactionCardQuery card,
        ISaveTransactionHandler save,
        IDeleteTransactionHandler delete,
        IAccountsQuery accounts,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(clock);

        _form = form;
        _card = card;
        _save = save;
        _delete = delete;
        _accounts = accounts;
        _clock = clock;

        OccurredOn = clock.Today;
    }

    /// <summary>Ключ правимой операции. Пусто — записывается новая.</summary>
    public Guid? Key { get; private set; }

    /// <summary>Правится существующая операция — её можно удалить.</summary>
    public bool IsExisting => Key is not null;

    /// <summary>Заголовок экрана.</summary>
    public string Title => IsExisting ? "Операция" : "Новая операция";

    /// <summary>Подписи видов для переключателя, в порядке <see cref="KindIndex"/>.</summary>
    public static IReadOnlyList<string> KindNames { get; } = ["Расход", "Доход", "Перевод"];

    /// <summary>Вид операции. По умолчанию расход — он записывается чаще всего.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindIndex))]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    [NotifyPropertyChangedFor(nameof(IsNotTransfer))]
    [NotifyPropertyChangedFor(nameof(SourceLabel))]
    public partial TransactionKind Kind { get; set; } = TransactionKind.Expense;

    /// <summary>Вид номером в переключателе.</summary>
    public int KindIndex
    {
        get => Array.IndexOf(KindOrder, Kind);
        set => Kind = KindOrder[Math.Clamp(value, 0, KindOrder.Length - 1)];
    }

    /// <summary>Перевод: второй счёт есть, категории и места нет.</summary>
    public bool IsTransfer => Kind is TransactionKind.Transfer;

    /// <summary>Доход или расход: категория обязательна, место по желанию.</summary>
    public bool IsNotTransfer => !IsTransfer;

    /// <summary>Подпись счёта списания: у перевода «Откуда», у остальных просто «Счёт».</summary>
    public string SourceLabel => IsTransfer ? "Откуда" : "Счёт";

    /// <summary>Сумма, как набрана: выражение из четырёх действий или число.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountPreview))]
    public partial string Amount { get; set; } = string.Empty;

    /// <summary>Сумма зачисления — у перевода между валютами.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAmountPreview))]
    public partial string TargetAmount { get; set; } = string.Empty;

    /// <summary>Вычисленный итог выражения в валюте счёта списания. Пусто — выражение не закончено.</summary>
    public string AmountPreview => Preview(Amount, SourceAccount?.Currency);

    /// <summary>Вычисленный итог суммы зачисления в валюте счёта зачисления.</summary>
    public string TargetAmountPreview => Preview(TargetAmount, TargetAccount?.Currency);

    /// <summary>Счета, которые форма предлагает: открытые и те, что уже в этой операции.</summary>
    public ObservableCollection<AccountOption> Accounts { get; } = [];

    /// <summary>Подкатегории вида операции.</summary>
    public ObservableCollection<CategoryOption> Categories { get; } = [];

    /// <summary>Места, подходящие под набранное: по первым буквам, от частых к редким.</summary>
    public ObservableCollection<PlaceOption> PlaceSuggestions { get; } = [];

    /// <summary>Счёт списания.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountPreview))]
    [NotifyPropertyChangedFor(nameof(NeedsTargetAmount))]
    [NotifyPropertyChangedFor(nameof(EarliestDate))]
    public partial AccountOption? SourceAccount { get; set; }

    /// <summary>Счёт зачисления — у перевода.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAmountPreview))]
    [NotifyPropertyChangedFor(nameof(NeedsTargetAmount))]
    [NotifyPropertyChangedFor(nameof(EarliestDate))]
    public partial AccountOption? TargetAccount { get; set; }

    /// <summary>Подкатегория — у дохода и расхода.</summary>
    [ObservableProperty]
    public partial CategoryOption? Category { get; set; }

    /// <summary>Место, как набрано. Совпавшее с существующим подставит его, новое заведётся при сохранении.</summary>
    [ObservableProperty]
    public partial string PlaceName { get; set; } = string.Empty;

    /// <summary>Дата операции.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OccurredOnDate))]
    public partial DateOnly OccurredOn { get; set; }

    /// <summary>Заметка.</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    /// <summary>Текст нарушенного правила. Пусто — сохранять можно.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Правило нарушено — сообщение показывается рядом с формой.</summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Вторая сумма нужна: перевод между счетами в разных валютах. При одной валюте
    /// поле не показывается — суммы обязаны совпадать, и спрашивать нечего.
    /// </summary>
    public bool NeedsTargetAmount =>
        IsTransfer && SourceAccount is { } source && TargetAccount is { } target && source.Currency != target.Currency;

    /// <summary>Дата операции в том виде, в каком её принимает календарь.</summary>
    public DateTime OccurredOnDate
    {
        get => OccurredOn.ToDateTime(TimeOnly.MinValue);
        set => OccurredOn = DateOnly.FromDateTime(value);
    }

    /// <summary>Раньше открытия счёта календарь не пускает; у перевода — позднейшего из двух.</summary>
    public DateTime EarliestDate
    {
        get
        {
            DateOnly earliest = Dates.Earliest;

            if (SourceAccount is { } source && source.OpenedOn > earliest)
            {
                earliest = source.OpenedOn;
            }

            if (IsTransfer && TargetAccount is { } target && target.OpenedOn > earliest)
            {
                earliest = target.OpenedOn;
            }

            return earliest.ToDateTime(TimeOnly.MinValue);
        }
    }

    /// <summary>Позже сегодняшнего дня календарь не пускает: операций в будущем нет.</summary>
    public DateTime LatestDate => _clock.Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Готовит форму: списки выбора, а для правки — саму операцию. Новой операции
    /// подставляется последний использованный счёт, если он открыт, либо счёт,
    /// с ленты которого пришли.
    /// </summary>
    /// <param name="key">Ключ правимой операции или <c>null</c> для новой.</param>
    /// <param name="accountKey">Счёт для подстановки в новую операцию — с чьей ленты пришли.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(Guid? key, Guid? accountKey = null, CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняются привязанные
        // коллекции, а их правка вне потока интерфейса роняет разметку
        TransactionForm form = await _form.ReadAsync(cancellationToken);

        _allAccounts = form.Accounts;
        _allCategories = form.Categories;
        _allPlaces = form.Places;

        TransactionCard? card = key is { } existing ? await _card.ReadAsync(existing, cancellationToken) : null;

        Key = card?.Key;
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(Title));

        FillAccounts(card);

        // Список категорий наполняется здесь, а не только сменой вида: у новой
        // операции вид уже расход, смены не происходит, и выбор остался бы пустым
        FillCategories();

        if (card is not null)
        {
            Kind = card.Kind;
            SourceAccount = Find(card.SourceAccountKey);
            TargetAccount = card.TargetAccountKey is { } target ? Find(target) : null;
            Amount = card.Amount.ToString(CultureInfo.InvariantCulture);
            TargetAmount = card.TargetAmount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            Category = Categories.FirstOrDefault(option => option.Key == card.CategoryKey);
            PlaceName = card.PlaceName ?? string.Empty;
            OccurredOn = card.OccurredOn;
            Note = card.Note ?? string.Empty;
        }
        else
        {
            // Закрытый последний счёт не подставляется: он не предлагается
            // и в выборе, а подставленный молча привёл бы к отказу при сохранении
            Guid? preset = accountKey ?? form.LastAccountKey;
            SourceAccount = preset is { } wanted ? Find(wanted) : null;
            SourceAccount ??= Accounts.FirstOrDefault();
        }

        RefreshSuggestions();
    }

    /// <summary>
    /// Сохраняет операцию. Нарушенное доменное правило показывается текстом рядом
    /// с формой — это ввод пользователя, а не сбой, и падать приложению не за что.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если операция сохранена и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        Error = null;

        if (SourceAccount is not { } source)
        {
            Error = "Выберите счёт";

            return false;
        }

        if (!AmountExpression.TryEvaluate(Amount, out decimal amount))
        {
            Error = "Сумма введена не полностью";

            return false;
        }

        decimal? targetAmount = null;

        if (NeedsTargetAmount)
        {
            if (!AmountExpression.TryEvaluate(TargetAmount, out decimal evaluated))
            {
                Error = "Сумма зачисления введена не полностью";

                return false;
            }

            targetAmount = evaluated;
        }

        if (IsTransfer && TargetAccount is null)
        {
            Error = "Выберите счёт зачисления";

            return false;
        }

        if (IsNotTransfer && Category is null)
        {
            Error = "Выберите категорию";

            return false;
        }

        try
        {
            await _save.HandleAsync(
                new SaveTransactionCommand
                {
                    Key = Key,
                    Kind = Kind,
                    SourceAccountKey = source.Key,
                    Amount = amount,
                    TargetAccountKey = IsTransfer ? TargetAccount?.Key : null,
                    TargetAmount = targetAmount,
                    CategoryKey = IsNotTransfer ? Category?.Key : null,
                    PlaceName = IsNotTransfer ? PlaceName : null,
                    OccurredOn = OccurredOn,
                    Note = Note
                },
                cancellationToken);

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;

            return false;
        }
    }

    /// <summary>
    /// Текст подтверждения удаления. Называет последствие — каким станет баланс
    /// счёта, — иначе пользователь подтверждает вслепую и проверяет результат потом.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task<string> DeletePromptAsync(CancellationToken cancellationToken = default)
    {
        if (Key is null || SourceAccount is not { } source)
        {
            return "Отменить удаление будет нельзя.";
        }

        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);

        // Балансы после удаления — из сохранённых сумм, а не из поля: поле могли
        // уже поправить, а удаляется операция в том виде, в каком она записана
        TransactionCard? card = await _card.ReadAsync(Key.Value, cancellationToken);

        if (card is null)
        {
            return "Отменить удаление будет нельзя.";
        }

        List<string> consequences = [];

        Money? sourceBalance = BalanceOf(accounts, card.SourceAccountKey);

        if (sourceBalance is { } current)
        {
            decimal delta = card.Kind is TransactionKind.Income ? -card.Amount : card.Amount;
            Money after = Money.Restore(current.Amount + delta, current.Currency);

            consequences.Add($"Баланс «{source.Name}» станет {after.Display}.");
        }

        if (card.TargetAccountKey is { } targetKey && card.TargetAmount is { } targetAmount
            && BalanceOf(accounts, targetKey) is { } targetBalance)
        {
            Money after = Money.Restore(targetBalance.Amount - targetAmount, targetBalance.Currency);
            string name = accounts.First(account => account.Key == targetKey).Name;

            consequences.Add($"Баланс «{name}» станет {after.Display}.");
        }

        consequences.Add("Отменить удаление будет нельзя.");

        return string.Join(' ', consequences);
    }

    /// <summary>Удаляет операцию. Подтверждение уже получено экраном.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если операция удалена и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key)
        {
            return false;
        }

        await _delete.HandleAsync(key, cancellationToken);

        return true;
    }

    /// <summary>Быстрый выбор: сегодня.</summary>
    [RelayCommand]
    public void SetToday() => OccurredOn = _clock.Today;

    /// <summary>Быстрый выбор: вчера — вчерашняя покупка записывается чаще прочих задним числом.</summary>
    [RelayCommand]
    public void SetYesterday() => OccurredOn = _clock.Today.AddDays(-1);

    /// <summary>Подставляет место из подсказки.</summary>
    /// <param name="place">Выбранное место.</param>
    public void PickPlace(PlaceOption place)
    {
        ArgumentNullException.ThrowIfNull(place);

        PlaceName = place.Name;
    }

    /// <summary>
    /// Смена вида стирает то, чего у нового вида не бывает: категорию и место
    /// у перевода, второй счёт у дохода и расхода. Скрытого состояния «до возврата
    /// прежнего вида» нет намеренно — оно уехало бы в базу незамеченным.
    /// </summary>
    partial void OnKindChanged(TransactionKind value)
    {
        FillCategories();

        if (value is TransactionKind.Transfer)
        {
            Category = null;
            PlaceName = string.Empty;
        }
        else
        {
            TargetAccount = null;
            TargetAmount = string.Empty;
        }

        OnPropertyChanged(nameof(NeedsTargetAmount));
        OnPropertyChanged(nameof(EarliestDate));
    }

    partial void OnPlaceNameChanged(string value) => RefreshSuggestions();

    /// <summary>Счета к выбору: открытые, плюс закрытые, на которых уже стоит правимая операция.</summary>
    private void FillAccounts(TransactionCard? card)
    {
        Accounts.Clear();

        foreach (AccountOption account in _allAccounts)
        {
            bool alreadyUsed = card is not null
                && (account.Key == card.SourceAccountKey || account.Key == card.TargetAccountKey);

            if (!account.IsClosed || alreadyUsed)
            {
                Accounts.Add(account);
            }
        }
    }

    /// <summary>Подкатегории вида операции. У перевода список пуст — категории у него нет.</summary>
    private void FillCategories()
    {
        CategoryOption? chosen = Category;

        Categories.Clear();

        if (IsTransfer)
        {
            return;
        }

        CategoryKind kind = Kind is TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;

        foreach (CategoryOption category in _allCategories)
        {
            if (category.Kind == kind)
            {
                Categories.Add(category);
            }
        }

        // Категория другого вида в новом списке не существует — сбрасывается
        Category = chosen is not null && Categories.Contains(chosen) ? chosen : null;
    }

    /// <summary>Подсказки мест: по вхождению набранного, от частых к редким, не больше горсти.</summary>
    private void RefreshSuggestions()
    {
        PlaceSuggestions.Clear();

        if (IsTransfer)
        {
            return;
        }

        ReadOnlySpan<char> typed = PlaceName.AsSpan().Trim();

        foreach (PlaceOption place in _allPlaces)
        {
            if (PlaceSuggestions.Count == SuggestionLimit)
            {
                break;
            }

            bool exact = Names.AreSame(place.Name, PlaceName);

            if (!exact && place.Name.AsSpan().Contains(typed, StringComparison.OrdinalIgnoreCase))
            {
                PlaceSuggestions.Add(place);
            }
        }
    }

    private AccountOption? Find(Guid key) => Accounts.FirstOrDefault(account => account.Key == key);

    private static Money? BalanceOf(IReadOnlyList<AccountListItem> accounts, Guid key)
    {
        foreach (AccountListItem account in accounts)
        {
            if (account.Key == key)
            {
                return account.Balance;
            }
        }

        return null;
    }

    private static string Preview(string expression, Currency? currency) =>
        currency is { } known && AmountExpression.TryEvaluate(expression, out decimal value)
            ? Money.Restore(value, known).Display
            : string.Empty;
}
