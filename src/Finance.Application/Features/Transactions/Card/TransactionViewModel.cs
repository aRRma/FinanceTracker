using Finance.Application.Texts;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Rules;
using Finance.Domain.Values;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Форма операции: запись и правка одним экраном. Самый частый сценарий приложения,
/// поэтому всё, что можно подставить, подставлено — последний счёт и сегодняшняя
/// дата, — а категория выбирается каждый раз: подставленная не глядя категория
/// портит отчёт молча.
/// </summary>
public sealed partial class TransactionViewModel : ObservableObject, IFormModel
{
    private static readonly TransactionKind[] KindOrder =
        [TransactionKind.Expense, TransactionKind.Income, TransactionKind.Transfer];

    private readonly ITransactionFormQuery _form;
    private readonly ITransactionCardQuery _card;
    private readonly ISaveTransactionHandler _save;
    private readonly IDeleteTransactionHandler _delete;
    private readonly IAccountsQuery _accounts;
    private readonly IClock _clock;
    private readonly TransactionPicks _picks;

    private IReadOnlyList<AccountOption> _allAccounts = [];
    private IReadOnlyList<CategoryOption> _allCategories = [];

    private Snapshot _saved;

    /// <summary>
    /// Создаёт модель представления формы операции.
    /// </summary>
    /// <param name="form">Списки выбора формы.</param>
    /// <param name="card">Чтение операции для правки.</param>
    /// <param name="save">Запись и правка операции.</param>
    /// <param name="delete">Удаление операции.</param>
    /// <param name="accounts">Балансы счетов — для текста подтверждения удаления.</param>
    /// <param name="clock">Часы: «сегодня» пользователя.</param>
    /// <param name="picks">Выбор, вернувшийся с экрана выбора счёта, категории или места.</param>
    public TransactionViewModel(
        ITransactionFormQuery form,
        ITransactionCardQuery card,
        ISaveTransactionHandler save,
        IDeleteTransactionHandler delete,
        IAccountsQuery accounts,
        IClock clock,
        TransactionPicks picks)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(picks);

        _form = form;
        _card = card;
        _save = save;
        _delete = delete;
        _accounts = accounts;
        _clock = clock;
        _picks = picks;

        OccurredOn = clock.Today;
    }

    /// <summary>
    /// Ключ правимой операции. Пусто — записывается новая.
    /// </summary>
    public Guid? Key { get; private set; }

    /// <summary>
    /// Правится существующая операция — её можно удалить.
    /// </summary>
    public bool IsExisting => Key is not null;

    /// <summary>
    /// Заголовок экрана.
    /// </summary>
    public string Title => IsExisting ? UiTexts.TransactionTitleExisting : UiTexts.TransactionTitleNew;

    /// <summary>
    /// Подписи видов для переключателя, в порядке <see cref="KindIndex"/>.
    /// </summary>
    public static IReadOnlyList<string> KindNames { get; } = [UiTexts.KindExpense, UiTexts.KindIncome, UiTexts.KindTransfer];

    /// <summary>
    /// Вид операции. По умолчанию расход — он записывается чаще всего.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindIndex))]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    [NotifyPropertyChangedFor(nameof(IsNotTransfer))]
    [NotifyPropertyChangedFor(nameof(SourceLabel))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial TransactionKind Kind { get; set; } = TransactionKind.Expense;

    /// <summary>
    /// Вид номером в переключателе.
    /// </summary>
    public int KindIndex
    {
        get => Array.IndexOf(KindOrder, Kind);
        set => Kind = KindOrder[Math.Clamp(value, 0, KindOrder.Length - 1)];
    }

    /// <summary>
    /// Перевод: второй счёт есть, категории и места нет.
    /// </summary>
    public bool IsTransfer => Kind is TransactionKind.Transfer;

    /// <summary>
    /// Доход или расход: категория обязательна, место по желанию.
    /// </summary>
    public bool IsNotTransfer => !IsTransfer;

    /// <summary>
    /// Подпись счёта списания: у перевода «Откуда», у остальных просто «Счёт».
    /// </summary>
    public string SourceLabel => IsTransfer ? UiTexts.TransactionSourceTransfer : UiTexts.TransactionSourceSimple;

    /// <summary>
    /// Вид категорий, подходящих операции: расходной — расходные. Тем же видом
    /// открывается экран выбора подкатегории.
    /// </summary>
    public CategoryKind CategoryKind => Kind is TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;

    /// <summary>
    /// Сумма, как набрана: выражение из четырёх действий или число.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountPreview))]
    [NotifyPropertyChangedFor(nameof(AmountDisplay))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string Amount { get; set; } = string.Empty;

    /// <summary>
    /// Сумма зачисления — у перевода между валютами.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAmountPreview))]
    [NotifyPropertyChangedFor(nameof(TargetAmountDisplay))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string TargetAmount { get; set; } = string.Empty;

    /// <summary>
    /// Какое поле суммы набирается: клавиатура в форме одна на оба.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceAmountActive))]
    [NotifyPropertyChangedFor(nameof(IsTargetAmountActive))]
    public partial AmountField ActiveAmount { get; private set; } = AmountField.Source;

    /// <summary>
    /// Клавиатура суммы на виду. Уходит, пока набирают заметку: там нужна буквенная
    /// клавиатура системы, и две сразу на экран не помещаются. Сохранение этим
    /// не управляется — оно стоит на панели заголовка.
    /// </summary>
    [ObservableProperty]
    public partial bool AreKeysVisible { get; set; } = true;

    /// <summary>
    /// Набирается сумма списания — её поле подсвечено.
    /// </summary>
    public bool IsSourceAmountActive => ActiveAmount is AmountField.Source;

    /// <summary>
    /// Набирается сумма зачисления.
    /// </summary>
    public bool IsTargetAmountActive => ActiveAmount is AmountField.Target;

    /// <summary>
    /// Набранное выражение для показа над итогом. Пустое поле — пустая строка: ноль показывает итог.
    /// </summary>
    public string AmountDisplay => Amount;

    /// <summary>
    /// Набранная сумма зачисления для показа.
    /// </summary>
    public string TargetAmountDisplay => TargetAmount;

    /// <summary>
    /// Итог выражения в валюте счёта списания. Показывается, только когда в поле
    /// уже число: пока действие не закрыто, итог даёт клавиша «=» — считать за
    /// пользователя раньше, чем он попросил, значит показывать не тот итог,
    /// который он набирает. Пустое поле показывает ноль в валюте: главная цифра
    /// формы не должна пропадать.
    /// </summary>
    public string AmountPreview => Preview(Amount, SourceAccount?.Currency);

    /// <summary>
    /// Итог суммы зачисления в валюте счёта зачисления — по тем же правилам.
    /// </summary>
    public string TargetAmountPreview => Preview(TargetAmount, TargetAccount?.Currency);

    /// <summary>
    /// Сохранять есть что: счёт выбран и суммы набраны до конца. Кнопка сохранения
    /// на панели заголовка гаснет, а не отказывает после нажатия. Незакрытое
    /// действие сохранению не мешает: записывается тот же итог, что показала бы «=».
    /// </summary>
    public bool CanSave =>
        !IsSaving
        && SourceAccount is not null
        && AmountExpression.TryEvaluate(Amount, out decimal amount)
        && amount > 0m
        && (!NeedsTargetAmount
            || (AmountExpression.TryEvaluate(TargetAmount, out decimal target) && target > 0m));

    /// <summary>
    /// Счета, которые форма предлагает: открытые и те, что уже в этой операции.
    /// </summary>
    public ObservableCollection<AccountOption> Accounts { get; } = [];

    /// <summary>
    /// Счета зачисления перевода: те же, кроме счёта списания. Отдельный список,
    /// а не общий: перевод на себя запрещён доменом, и предлагать его в выборе —
    /// значит рассказывать о запрете уже после нажатия «Сохранить».
    /// </summary>
    public ObservableCollection<AccountOption> TargetAccounts { get; } = [];

    /// <summary>
    /// Подкатегории вида операции.
    /// </summary>
    public ObservableCollection<CategoryOption> Categories { get; } = [];

    /// <summary>
    /// Счёт списания.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountPreview))]
    [NotifyPropertyChangedFor(nameof(NeedsTargetAmount))]
    [NotifyPropertyChangedFor(nameof(EarliestDate))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(SourceAccountCaption))]
    public partial AccountOption? SourceAccount { get; set; }

    /// <summary>
    /// Счёт зачисления — у перевода.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAmountPreview))]
    [NotifyPropertyChangedFor(nameof(NeedsTargetAmount))]
    [NotifyPropertyChangedFor(nameof(EarliestDate))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(TargetAccountCaption))]
    public partial AccountOption? TargetAccount { get; set; }

    /// <summary>
    /// Подкатегория — у дохода и расхода.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryCaption))]
    [NotifyPropertyChangedFor(nameof(HasCategory))]
    public partial CategoryOption? Category { get; set; }

    /// <summary>
    /// Место, как набрано. Совпавшее с существующим подставит его, новое заведётся при сохранении.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceCaption))]
    [NotifyPropertyChangedFor(nameof(HasPlace))]
    public partial string PlaceName { get; set; } = string.Empty;

    /// <summary>
    /// Счёт списания в строке-поле: название, пока не выбран — приглашение выбрать.
    /// </summary>
    public string SourceAccountCaption => SourceAccount?.Name ?? UiTexts.CommonChoose;

    /// <summary>
    /// Счёт зачисления в строке-поле.
    /// </summary>
    public string TargetAccountCaption => TargetAccount?.Name ?? UiTexts.CommonChoose;

    /// <summary>
    /// Подкатегория в строке-поле: только выбранное название, без группы —
    /// строка-поле показывает выбор, а не всю вложенность дерева категорий.
    /// </summary>
    public string CategoryCaption => Category is { } category
        ? category.Name
        : UiTexts.CommonChoose;

    /// <summary>
    /// Место в строке-поле. Место необязательно, и пустое так и подписано.
    /// </summary>
    public string PlaceCaption => PlaceName.Length > 0 ? PlaceName : UiTexts.CommonOptional;

    /// <summary>
    /// Категория выбрана: иначе строка показывает приглашение приглушённо.
    /// </summary>
    public bool HasCategory => Category is not null;

    /// <summary>
    /// Место указано.
    /// </summary>
    public bool HasPlace => PlaceName.Length > 0;

    /// <summary>
    /// Дата операции.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OccurredOnDate))]
    [NotifyPropertyChangedFor(nameof(OccurredOnCaption))]
    [NotifyPropertyChangedFor(nameof(IsToday))]
    [NotifyPropertyChangedFor(nameof(IsYesterday))]
    public partial DateOnly OccurredOn { get; set; }

    /// <summary>
    /// Дата операции словами: «Сегодня», «Вчера», иначе «15 сентября» — с годом, если год не этот.
    /// </summary>
    public string OccurredOnCaption =>
        IsToday ? UiTexts.TransactionToday
        : IsYesterday ? UiTexts.TransactionYesterday
        : DateText.DayWithYearIfOther(OccurredOn, _clock.Today);

    /// <summary>
    /// Выбрана сегодняшняя дата — её чип подсвечен.
    /// </summary>
    public bool IsToday => OccurredOn == _clock.Today;

    /// <summary>
    /// Выбрана вчерашняя дата.
    /// </summary>
    public bool IsYesterday => OccurredOn == _clock.Today.AddDays(-1);

    /// <summary>
    /// Заметка.
    /// </summary>
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    /// <summary>
    /// Текст нарушенного правила. Пусто — сохранять можно.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Правило нарушено — сообщение показывается рядом с формой.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <inheritdoc />
    public bool IsDirty => Take() != _saved;

    /// <summary>
    /// Правимые поля формы одним значением. Запись сравнивается сама, по всем полям
    /// сразу: список «что считать правкой» отдельно от полей разошёлся бы с ними
    /// при первом же новом поле.
    /// </summary>
    private Snapshot Take() => new(
        Kind,
        SourceAccount?.Key,
        TargetAccount?.Key,
        Amount,
        TargetAmount,
        Category?.Key,
        PlaceName,
        OccurredOn,
        Note);

    /// <summary>
    /// Состояние формы, с которым сверяется уход с экрана.
    /// </summary>
    private readonly record struct Snapshot(
        TransactionKind Kind,
        Guid? SourceAccount,
        Guid? TargetAccount,
        string Amount,
        string TargetAmount,
        Guid? Category,
        string PlaceName,
        DateOnly OccurredOn,
        string Note);

    /// <summary>
    /// Идёт сохранение или удаление. Кнопки зовут методы напрямую, минуя команду
    /// с её защитой от повторного запуска: без флага второе нажатие до ухода
    /// экрана записало бы операцию дважды.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// Вторая сумма нужна: перевод между счетами в разных валютах. При одной валюте
    /// поле не показывается — суммы обязаны совпадать, и спрашивать нечего.
    /// </summary>
    public bool NeedsTargetAmount =>
        IsTransfer && SourceAccount is { } source && TargetAccount is { } target && source.Currency != target.Currency;

    /// <summary>
    /// Дата операции в том виде, в каком её принимает календарь.
    /// </summary>
    public DateTime OccurredOnDate
    {
        get => OccurredOn.ToDateTime(TimeOnly.MinValue);
        set => OccurredOn = DateOnly.FromDateTime(value);
    }

    /// <summary>
    /// Раньше открытия счёта календарь не пускает; у перевода — позднейшего из двух.
    /// </summary>
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

    /// <summary>
    /// Позже сегодняшнего дня календарь не пускает: операций в будущем нет.
    /// </summary>
    public DateTime LatestDate => _clock.Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Готовит форму: списки выбора, а для правки — саму операцию. Новой операции
    /// подставляется последний использованный счёт, если он открыт, либо счёт,
    /// с ленты которого пришли.
    /// </summary>
    /// <param name="key">Ключ правимой операции или <c>null</c> для новой.</param>
    /// <param name="accountKey">Счёт для подстановки в новую операцию — с чьей ленты пришли.</param>
    /// <param name="kind">
    /// Вид новой операции: им приходят с ярлыка на значке приложения, где вид
    /// уже выбран. У правки вид берётся из самой записи, и параметр не действует.
    /// </param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(
        Guid? key,
        Guid? accountKey = null,
        TransactionKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняются привязанные
        // коллекции, а их правка вне потока интерфейса роняет разметку
        TransactionForm form = await _form.ReadAsync(cancellationToken);

        _allAccounts = form.Accounts;
        _allCategories = form.Categories;

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
            Amount = card.Amount.ToString(CultureInfo.CurrentCulture);
            TargetAmount = card.TargetAmount?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            Category = Categories.FirstOrDefault(option => option.Key == card.CategoryKey);
            PlaceName = card.PlaceName ?? string.Empty;
            OccurredOn = card.OccurredOn;
            Note = card.Note ?? string.Empty;
        }
        else
        {
            // Вид ставится до снимка: после него форма считалась бы правленой,
            // и уход с неё, к которой не притронулись, спрашивал бы подтверждение.
            // Смена вида сама пересоберёт списки категорий и счетов зачисления
            if (kind is { } wantedKind)
            {
                Kind = wantedKind;
            }

            // Закрытый последний счёт не подставляется: он не предлагается
            // и в выборе, а подставленный молча привёл бы к отказу при сохранении
            Guid? preset = accountKey ?? form.LastAccountKey;
            SourceAccount = preset is { } wanted ? Find(wanted) : null;
            SourceAccount ??= Accounts.FirstOrDefault();
        }

        _saved = Take();
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
        if (IsSaving)
        {
            return false;
        }

        Error = null;

        if (SourceAccount is not { } source)
        {
            Error = UiTexts.TransactionChooseAccount;

            return false;
        }

        if (!AmountExpression.TryEvaluate(Amount, out decimal amount))
        {
            Error = UiTexts.TransactionAmountIncomplete;

            return false;
        }

        decimal? targetAmount = null;

        if (NeedsTargetAmount)
        {
            if (!AmountExpression.TryEvaluate(TargetAmount, out decimal evaluated))
            {
                Error = UiTexts.TransactionTargetAmountIncomplete;

                return false;
            }

            targetAmount = evaluated;
        }

        if (IsTransfer && TargetAccount is null)
        {
            Error = UiTexts.TransactionChooseTargetAccount;

            return false;
        }

        if (IsNotTransfer && Category is null)
        {
            Error = UiTexts.TransactionChooseCategory;

            return false;
        }

        IsSaving = true;
        bool done = false;

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

            done = true;

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;

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
    /// Текст подтверждения удаления. Называет последствие — каким станет баланс
    /// счёта, — иначе пользователь подтверждает вслепую и проверяет результат потом.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task<string> DeletePromptAsync(CancellationToken cancellationToken = default)
    {
        if (Key is null)
        {
            return UiTexts.TransactionDeleteIrreversible;
        }

        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);

        // Балансы после удаления — из сохранённых сумм, а не из поля: поле могли
        // уже поправить, а удаляется операция в том виде, в каком она записана
        TransactionCard? card = await _card.ReadAsync(Key.Value, cancellationToken);

        if (card is null)
        {
            return UiTexts.TransactionDeleteIrreversible;
        }

        List<string> consequences = [];

        Money? sourceBalance = BalanceOf(accounts, card.SourceAccountKey);

        if (sourceBalance is { } current)
        {
            decimal delta = card.Kind is TransactionKind.Income ? -card.Amount : card.Amount;
            Money after = Money.Restore(current.Amount + delta, current.Currency);

            // Название берётся у записанного счёта, а не у выбранного в форме:
            // счёт в поле могли уже сменить, а удаляется операция как записана,
            // и подпись разошлась бы с числом рядом с ней
            string name = accounts.First(account => account.Key == card.SourceAccountKey).Name;

            consequences.Add(string.Format(UiCulture.Current, UiTexts.TransactionBalanceAfter, name, after.Display));
        }

        if (card.TargetAccountKey is { } targetKey && card.TargetAmount is { } targetAmount
            && BalanceOf(accounts, targetKey) is { } targetBalance)
        {
            Money after = Money.Restore(targetBalance.Amount - targetAmount, targetBalance.Currency);
            string name = accounts.First(account => account.Key == targetKey).Name;

            consequences.Add(string.Format(UiCulture.Current, UiTexts.TransactionBalanceAfter, name, after.Display));
        }

        consequences.Add(UiTexts.TransactionDeleteIrreversible);

        return string.Join(' ', consequences);
    }

    /// <summary>
    /// Удаляет операцию. Подтверждение уже получено экраном.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если операция удалена и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key || IsSaving)
        {
            return false;
        }

        IsSaving = true;
        bool done = false;

        try
        {
            await _delete.HandleAsync(key, cancellationToken);

            done = true;

            return true;
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
    /// Нажата клавиша суммы: цифра, запятая или знак действия.
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

        Edit(expression => AmountInput.Append(expression, key[0]));
    }

    /// <summary>
    /// Стирает последний набранный знак.
    /// </summary>
    [RelayCommand]
    public void Backspace() => Edit(AmountInput.Backspace);

    /// <summary>
    /// Сворачивает набранное выражение в итог — клавиша «=». Считает то поле,
    /// которое набирается: у перевода между валютами их два.
    /// </summary>
    [RelayCommand]
    public void Evaluate() => Edit(AmountInput.Collapse);

    /// <summary>
    /// Переводит клавиатуру на сумму списания.
    /// </summary>
    [RelayCommand]
    public void ActivateSourceAmount()
    {
        ActiveAmount = AmountField.Source;
        AreKeysVisible = true;
    }

    /// <summary>
    /// Переводит клавиатуру на сумму зачисления.
    /// </summary>
    [RelayCommand]
    public void ActivateTargetAmount()
    {
        // Поля зачисления может не быть на экране: перевод между счетами одной
        // валюты его не показывает, и уводить туда набор не во что
        if (!NeedsTargetAmount)
        {
            return;
        }

        ActiveAmount = AmountField.Target;
        AreKeysVisible = true;
    }

    /// <summary>
    /// Правит то поле суммы, которое набирается сейчас.
    /// </summary>
    private void Edit(Func<string, string> change)
    {
        if (ActiveAmount is AmountField.Target && NeedsTargetAmount)
        {
            TargetAmount = change(TargetAmount);
        }
        else
        {
            Amount = change(Amount);
        }
    }

    /// <summary>
    /// Быстрый выбор: сегодня.
    /// </summary>
    [RelayCommand]
    public void SetToday() => OccurredOn = _clock.Today;

    /// <summary>
    /// Быстрый выбор: вчера — вчерашняя покупка записывается чаще прочих задним числом.
    /// </summary>
    [RelayCommand]
    public void SetYesterday() => OccurredOn = _clock.Today.AddDays(-1);

    /// <summary>
    /// Забирает выбор, сделанный на экране счёта, категории или места. Зовётся
    /// при каждом появлении формы: экран выбора уходит обычным «назад», и другого
    /// места узнать о его решении у формы нет. Пустой выбор ничего не меняет —
    /// возврат без выбора оставляет форму как была.
    /// </summary>
    public void ApplyPicks()
    {
        if (_picks.Account is { } account && Find(account) is { } chosen)
        {
            SourceAccount = chosen;
        }

        if (_picks.TargetAccount is { } target
            && TargetAccounts.FirstOrDefault(option => option.Key == target) is { } chosenTarget)
        {
            TargetAccount = chosenTarget;
        }

        if (_picks.Category is { } category
            && Categories.FirstOrDefault(option => option.Key == category) is { } chosenCategory)
        {
            Category = chosenCategory;
        }

        if (_picks.PlaceName is { } place)
        {
            PlaceName = place;
        }

        _picks.Clear();
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
            ActiveAmount = AmountField.Source;
        }

        OnPropertyChanged(nameof(NeedsTargetAmount));
        OnPropertyChanged(nameof(EarliestDate));
    }

    /// <summary>
    /// Счета к выбору: открытые, плюс закрытые, на которых уже стоит правимая операция.
    /// </summary>
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

        FillTargetAccounts();
    }

    /// <summary>
    /// Счета зачисления: всё, кроме выбранного счёта списания.
    /// </summary>
    private void FillTargetAccounts()
    {
        TargetAccounts.Clear();

        foreach (AccountOption account in Accounts)
        {
            if (SourceAccount is not { } source || account.Key != source.Key)
            {
                TargetAccounts.Add(account);
            }
        }
    }

    /// <summary>
    /// Смена счёта списания перестраивает список зачисления: счёт, ставший счётом
    /// списания, обязан уйти из выбора, а если он там уже стоял — сброситься.
    /// </summary>
    partial void OnSourceAccountChanged(AccountOption? value)
    {
        if (value is { } source && TargetAccount is { } target && target.Key == source.Key)
        {
            TargetAccount = null;
        }

        FillTargetAccounts();
    }

    /// <summary>
    /// Подкатегории вида операции. У перевода список пуст — категории у него нет.
    /// </summary>
    private void FillCategories()
    {
        CategoryOption? chosen = Category;

        Categories.Clear();

        if (IsTransfer)
        {
            return;
        }

        CategoryKind kind = CategoryKind;

        foreach (CategoryOption category in _allCategories)
        {
            // Универсальная группа принимает оба вида: без этого выбранный на
            // экране выбора возврат в расходной статье в список не попадал бы,
            // а форма молча оставалась бы без категории
            if (category.Accepts(kind))
            {
                Categories.Add(category);
            }
        }

        // Категория другого вида в новом списке не существует — сбрасывается
        Category = chosen is not null && Categories.Contains(chosen) ? chosen : null;
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
        currency is not { } known ? string.Empty
        : expression.Length is 0 ? Money.Restore(0m, known).Display
        : AmountInput.HasOperation(expression) ? string.Empty
        : AmountExpression.TryEvaluate(expression, out decimal value) ? Money.Restore(value, known).Display
        : string.Empty;
}
