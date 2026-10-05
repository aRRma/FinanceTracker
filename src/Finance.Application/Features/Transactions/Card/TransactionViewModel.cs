using Finance.Application.Texts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Deletion;
using Finance.Domain.Enums;
using Finance.Domain.Rules;
using Finance.Domain.Values;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Форма операции: запись и правка одним экраном. Самый частый сценарий приложения,
/// поэтому всё, что можно подставить, подставлено — счёт по умолчанию и сегодняшняя
/// дата, — а категория выбирается каждый раз: подставленная не глядя категория
/// портит отчёт молча.
/// </summary>
public sealed partial class TransactionViewModel : FormViewModel
{
    // Значки невыбранных полей — ключи набора значков интерфейса
    private const string NoAccountIcon = "wallet";
    private const string NoCategoryIcon = "tag";

    private readonly ITransactionFormQuery _form;
    private readonly ITransactionCardQuery _card;
    private readonly ISaveTransactionHandler _save;
    private readonly IDeleteTransactionsHandler _delete;
    private readonly ITransactionDeletionQuery _deletion;
    private readonly IClock _clock;
    private readonly TransactionPicks _picks;
    private readonly IChangeNotifier _changes;

    private IReadOnlyList<AccountOption> _allAccounts = [];
    private IReadOnlyList<CategoryOption> _allCategories = [];

    // Правимая операция: её счета остаются в выборе, даже заблокированные
    private TransactionCard? _editing;

    // Номер изменения счетов, с которым прочитан список, и поле, ждущее счёт,
    // заведённый из формы: true — счёт зачисления, false — списания
    private long _accountsVersion;
    private bool? _awaitedAccountIsTarget;

    private Snapshot _saved;

    /// <summary>
    /// Создаёт модель представления формы операции.
    /// </summary>
    /// <param name="form">Списки выбора формы.</param>
    /// <param name="card">Чтение операции для правки.</param>
    /// <param name="save">Запись и правка операции.</param>
    /// <param name="delete">Удаление операции.</param>
    /// <param name="deletion">Последствия удаления — для текста подтверждения.</param>
    /// <param name="clock">Часы: «сегодня» пользователя.</param>
    /// <param name="picks">Выбор, вернувшийся с экрана выбора счёта, категории или места.</param>
    /// <param name="changes">Номера изменений данных — узнать, заведён ли счёт, пока форма ждала.</param>
    public TransactionViewModel(
        ITransactionFormQuery form,
        ITransactionCardQuery card,
        ISaveTransactionHandler save,
        IDeleteTransactionsHandler delete,
        ITransactionDeletionQuery deletion,
        IClock clock,
        TransactionPicks picks,
        IChangeNotifier changes)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(deletion);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(changes);

        _form = form;
        _card = card;
        _save = save;
        _delete = delete;
        _deletion = deletion;
        _clock = clock;
        _picks = picks;
        _changes = changes;

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
    /// Вид операции. По умолчанию расход — он записывается чаще всего.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    [NotifyPropertyChangedFor(nameof(IsNotTransfer))]
    [NotifyPropertyChangedFor(nameof(SourceLabel))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(AmountHero))]
    [NotifyPropertyChangedFor(nameof(AmountTone))]
    public partial TransactionKind Kind { get; set; } = TransactionKind.Expense;

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
    [NotifyPropertyChangedFor(nameof(AmountDisplay))]
    [NotifyPropertyChangedFor(nameof(HasAmountOperation))]
    [NotifyPropertyChangedFor(nameof(AmountHero))]
    [NotifyPropertyChangedFor(nameof(AmountTone))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string Amount { get; set; } = string.Empty;

    /// <summary>
    /// Сумма зачисления — у перевода между валютами.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAmountDisplay))]
    [NotifyPropertyChangedFor(nameof(HasTargetAmountOperation))]
    [NotifyPropertyChangedFor(nameof(TargetAmountHero))]
    [NotifyPropertyChangedFor(nameof(TargetAmountTone))]
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
    /// Набранное выражение для показа над итогом — когда в нём есть знак действия.
    /// </summary>
    public string AmountDisplay => Amount;

    /// <summary>
    /// Набранная сумма зачисления для показа.
    /// </summary>
    public string TargetAmountDisplay => TargetAmount;

    /// <summary>
    /// В сумме набран знак действия. Только тогда над итогом показано выражение:
    /// у простого числа оно повторяло бы итог.
    /// </summary>
    public bool HasAmountOperation => AmountInput.HasOperation(Amount);

    /// <summary>
    /// В сумме зачисления набран знак действия.
    /// </summary>
    public bool HasTargetAmountOperation => AmountInput.HasOperation(TargetAmount);

    /// <summary>
    /// Главная цифра формы: итог в валюте счёта списания со знаком вида — расход
    /// с минусом, доход с плюсом, перевод без знака, как в ленте. Пустое поле
    /// показывает ноль: главная цифра формы не должна пропадать. Пока счёт не выбран,
    /// валюты нет и число идёт без её знака — пустое место вместо набранного
    /// читалось бы как неработающая клавиатура.
    /// Пока действие не закрыто, вместо итога — подсказка про «=»: считать за
    /// пользователя раньше, чем он попросил, значит показывать не тот итог,
    /// который он набирает, а минус вида перед выражением читался бы вычитанием.
    /// </summary>
    public string AmountHero => Hero(Amount, SourceAccount?.Currency, Kind);

    /// <summary>
    /// Каким тоном показана главная цифра формы.
    /// </summary>
    public AmountTone AmountTone => Tone(Amount, Kind);

    /// <summary>
    /// Сумма зачисления крупно. Знака у неё нет: зачисление бывает только у перевода.
    /// </summary>
    public string TargetAmountHero => Hero(TargetAmount, TargetAccount?.Currency, TransactionKind.Transfer);

    /// <summary>
    /// Каким тоном показана сумма зачисления.
    /// </summary>
    public AmountTone TargetAmountTone => Tone(TargetAmount, TransactionKind.Transfer);

    /// <summary>
    /// Сохранять есть что: суммы набраны до конца. Без набранной суммы кнопка
    /// сохранения гаснет, а невыбранный счёт или категория её не гасят: погашенная
    /// кнопка не говорит, чего не хватает, а нажатие называет это.
    /// Незакрытое действие сохранению не мешает: записывается тот же итог, что показала бы «=».
    /// </summary>
    public override bool CanSave =>
        IsIdle
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
    [NotifyPropertyChangedFor(nameof(AmountHero))]
    [NotifyPropertyChangedFor(nameof(AmountTone))]
    [NotifyPropertyChangedFor(nameof(NeedsTargetAmount))]
    [NotifyPropertyChangedFor(nameof(EarliestDate))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(SourceAccountCaption))]
    [NotifyPropertyChangedFor(nameof(SourceAccountIcon))]
    [NotifyPropertyChangedFor(nameof(HasSourceAccount))]
    public partial AccountOption? SourceAccount { get; set; }

    /// <summary>
    /// Счёт списания выбран. Иначе поле отмечено как обязательное: без счёта
    /// операцию не записать, а подставить нечего — счетов нет или все заблокированы.
    /// </summary>
    public bool HasSourceAccount => SourceAccount is not null;

    /// <summary>
    /// Выбирать счёт не из чего: незаблокированных счетов нет. Касание поля ведёт тогда
    /// сразу в карточку нового счёта, а не на пустой экран выбора.
    /// </summary>
    public bool NeedsNewAccount => Accounts.Count is 0;

    /// <summary>
    /// Счёт зачисления выбирать не из чего: кроме счёта списания, незаблокированных счетов нет.
    /// </summary>
    public bool NeedsNewTargetAccount => TargetAccounts.Count is 0;

    /// <summary>
    /// Счёт зачисления — у перевода.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAmountHero))]
    [NotifyPropertyChangedFor(nameof(TargetAmountTone))]
    [NotifyPropertyChangedFor(nameof(NeedsTargetAmount))]
    [NotifyPropertyChangedFor(nameof(EarliestDate))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(TargetAccountCaption))]
    [NotifyPropertyChangedFor(nameof(TargetAccountIcon))]
    [NotifyPropertyChangedFor(nameof(HasTargetAccount))]
    public partial AccountOption? TargetAccount { get; set; }

    /// <summary>
    /// Счёт зачисления выбран: иначе поле показывает приглашение цветом действия.
    /// </summary>
    public bool HasTargetAccount => TargetAccount is not null;

    /// <summary>
    /// Подкатегория — у дохода и расхода.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryCaption))]
    [NotifyPropertyChangedFor(nameof(CategoryIcon))]
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
    /// Счёт списания в строке-поле: название, пока не выбран — приглашение выбрать,
    /// а если выбирать не из чего — добавить.
    /// </summary>
    public string SourceAccountCaption =>
        SourceAccount?.Name ?? (NeedsNewAccount ? UiTexts.TransactionAccountAdd : UiTexts.CommonChoose);

    /// <summary>
    /// Счёт зачисления в строке-поле.
    /// </summary>
    public string TargetAccountCaption =>
        TargetAccount?.Name ?? (NeedsNewTargetAccount ? UiTexts.TransactionAccountAdd : UiTexts.CommonChoose);

    /// <summary>
    /// Подкатегория в строке-поле: только выбранное название, без группы —
    /// строка-поле показывает выбор, а не всю вложенность дерева категорий.
    /// Кроме «Прочего»: оно есть в каждой группе и без неё ничего не говорит.
    /// </summary>
    public string CategoryCaption => Category is { } category
        ? category.Caption
        : UiTexts.CommonChoose;

    /// <summary>
    /// Место в строке-поле. Место необязательно, и пустое так и подписано.
    /// </summary>
    public string PlaceCaption => PlaceName.Length > 0 ? PlaceName : UiTexts.CommonOptional;

    /// <summary>
    /// Значок счёта списания — тот же, что у счёта в балансах и в выборе. Пока счёт
    /// не выбран — общий значок кошелька.
    /// </summary>
    public string SourceAccountIcon => SourceAccount?.Icon ?? NoAccountIcon;

    /// <summary>
    /// Значок счёта зачисления.
    /// </summary>
    public string TargetAccountIcon => TargetAccount?.Icon ?? NoAccountIcon;

    /// <summary>
    /// Значок выбранной подкатегории, пока не выбрана — общий значок метки.
    /// </summary>
    public string CategoryIcon => Category?.Icon ?? NoCategoryIcon;

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

    /// <inheritdoc />
    public override bool IsDirty => Take() != _saved;

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
    public DateTime EarliestDate => Earliest().ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Смена счёта поменяла в форме то, чего пользователь не трогал: дату или валюту,
    /// либо сохранение не состоялось — не выбран счёт или категория. Сообщение
    /// показывается коротко и не требует ответа: без него операция молча ушла бы
    /// другим днём или в другой валюте, а нажатие «Сохранить» ничего бы не сделало.
    /// </summary>
    public event Action<string>? Notified;

    /// <summary>
    /// Раньше какого дня операцию записать нельзя. Счёта нет — граница общая для всех дат.
    /// </summary>
    private DateOnly Earliest()
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

        return earliest;
    }

    /// <summary>
    /// Смена счёта могла оставить дату раньше его открытия. Дата переносится здесь,
    /// до того как календарь узнает новую границу: иначе её молча сдвинул бы он сам.
    /// Имени счёта в сообщении нет: счёт пользователь только что выбрал сам, а длинное
    /// имя не влезло бы в две строки всплывающего сообщения.
    /// </summary>
    private void KeepDateAfterOpening()
    {
        DateOnly earliest = Earliest();

        if (OccurredOn >= earliest)
        {
            return;
        }

        OccurredOn = earliest;
        Notified?.Invoke(string.Format(
            UiCulture.Current,
            UiTexts.TransactionDateMoved,
            DateText.DayWithYearIfOther(earliest, _clock.Today)));
    }

    /// <summary>
    /// Позже сегодняшнего дня календарь не пускает: операций в будущем нет.
    /// </summary>
    public DateTime LatestDate => _clock.Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Готовит форму: списки выбора, а для правки — саму операцию. Новой операции
    /// подставляется счёт, с ленты которого пришли, если он не заблокирован, иначе
    /// счёт по умолчанию.
    /// </summary>
    /// <param name="key">Ключ правимой операции или <c>null</c> для новой.</param>
    /// <param name="accountKey">Счёт для подстановки в новую операцию — с чьей ленты пришли.</param>
    /// <param name="kind">Вид новой операции — с ярлыка на значке приложения; у правки вид берётся из самой записи.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(
        Guid? key,
        Guid? accountKey = null,
        TransactionKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        // Номер берётся до чтения: счёт, заведённый во время чтения, даст номер
        // новее, и на возврате список перечитается ещё раз, а не потеряет его
        _accountsVersion = _changes.VersionOf(DataChange.Accounts);

        // ConfigureAwait(false) здесь недопустим: следом наполняются привязанные
        // коллекции, а их правка вне потока интерфейса роняет разметку
        TransactionForm form = await _form.ReadAsync(cancellationToken);

        _allAccounts = form.Accounts;
        _allCategories = form.Categories;

        TransactionCard? card = key is { } existing ? await _card.ReadAsync(existing, cancellationToken) : null;

        _editing = card;
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
            // Тем же путём, что итог «=»: сумма встаёт в поле так, как её набирают,
            // без хвоста нулей — иначе «1500,00» показало бы копейки, которых не набирали,
            // и не дало бы дописать цифру
            Amount = AmountInput.Write(card.Amount);
            TargetAmount = card.TargetAmount is { } credited ? AmountInput.Write(credited) : string.Empty;
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

            // Из ленты счёта — этот счёт, иначе счёт по умолчанию. Лента
            // заблокированного счёта тоже уступает счёту по умолчанию: заблокированный
            // не предлагается и в выборе, а подставленный привёл бы к отказу при сохранении
            SourceAccount = (accountKey is { } fromFeed ? Find(fromFeed) : null)
                ?? (form.DefaultAccountKey is { } byDefault ? Find(byDefault) : null);
        }

        _saved = Take();
    }

    /// <summary>
    /// Сохраняет операцию.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если операция сохранена и экран можно закрыть.</returns>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
        {
            return false;
        }

        // Сброс до проверок: прежняя карточка рядом со всплывающим сообщением говорила бы не о том
        Error = null;

        // Невыбранное поле — счёт или категория — называется всплывающим
        // сообщением, а не карточкой над клавиатурой: правило не нарушено, поле
        // и так отмечено, и сообщение лишь отвечает на нажатие, которое ничего
        // не сделало. Карточка остаётся нарушенным правилам и недобранной сумме
        if (SourceAccount is not { } source)
        {
            Notified?.Invoke(NeedsNewAccount ? UiTexts.TransactionAddAccountFirst : UiTexts.TransactionChooseAccount);

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
            Notified?.Invoke(NeedsNewTargetAccount
                ? UiTexts.TransactionAddTargetAccountFirst
                : UiTexts.TransactionChooseTargetAccount);

            return false;
        }

        if (IsNotTransfer && Category is null)
        {
            Notified?.Invoke(UiTexts.TransactionChooseCategory);

            return false;
        }

        SaveTransactionCommand command = new()
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
        };

        return await WriteAsync(token => _save.HandleAsync(command, token), cancellationToken);
    }

    /// <summary>
    /// Последствия удаления для диалога подтверждения: каким станет баланс счёта.
    /// Читаются по записанной операции, а не по полям: поля могли уже поправить,
    /// а удаляется операция в том виде, в каком она записана. Пусто — операция новая.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task<TransactionDeletion?> DeletePromptAsync(CancellationToken cancellationToken = default) =>
        Key is { } key ? await _deletion.ReadAsync([key], cancellationToken) : null;

    /// <summary>
    /// Удаляет операцию. Подтверждение уже получено экраном.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если операция удалена и экран можно закрыть.</returns>
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default) =>
        Key is { } key && await WriteAsync(token => _delete.HandleAsync([key], token), cancellationToken);

    /// <summary>
    /// Нажата клавиша суммы: цифра, запятая или знак действия.
    /// </summary>
    /// <param name="key">Знак на клавише.</param>
    /// <exception cref="ArgumentOutOfRangeException">Клавиши с таким знаком на клавиатуре нет.</exception>
    [RelayCommand]
    public void PressKey(char key) => Edit(expression => AmountInput.Append(expression, key));

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
    /// Форма уходит за счётом — в карточку нового счёта или на экран выбора, где
    /// новый счёт тоже можно завести. Заведённый счёт встанет в то поле, ради
    /// которого уходили, — набранная сумма при этом остаётся.
    /// </summary>
    /// <param name="target">Счёт нужен для зачисления перевода, а не для списания.</param>
    public void AwaitNewAccount(bool target) => _awaitedAccountIsTarget = target;

    /// <summary>
    /// Забирает счёт, заведённый, пока форма ждала: перечитывает список счетов и ставит
    /// новый счёт в ждавшее поле. Форма при возврате не перечитывается целиком —
    /// стёрлось бы набранное, — а без перечитывания нового счёта нет в её списке.
    /// Вернулись без нового счёта — ничего не меняется.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task TakeNewAccountAsync(CancellationToken cancellationToken = default)
    {
        bool? target = _awaitedAccountIsTarget;
        _awaitedAccountIsTarget = null;

        long version = _changes.VersionOf(DataChange.Accounts);

        if (target is not { } forTarget || version == _accountsVersion)
        {
            return;
        }

        _accountsVersion = version;

        HashSet<Guid> known = [.. _allAccounts.Select(static account => account.Key)];

        // ConfigureAwait(false) недопустим: следом правятся привязанные коллекции
        TransactionForm form = await _form.ReadAsync(cancellationToken);

        _allAccounts = form.Accounts;
        FillAccounts(_editing);

        AccountOption? added = Accounts.FirstOrDefault(account => !known.Contains(account.Key));

        if (added is null)
        {
            return;
        }

        if (!forTarget)
        {
            SourceAccount = added;
        }
        else if (IsTransfer && TargetAccounts.Contains(added))
        {
            TargetAccount = added;
        }
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

        KeepDateAfterOpening();
        OnPropertyChanged(nameof(NeedsTargetAmount));
        OnPropertyChanged(nameof(EarliestDate));
    }

    /// <summary>
    /// Счета к выбору: открытые, плюс заблокированные, на которых уже стоит правимая операция.
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

        OnPropertyChanged(nameof(NeedsNewAccount));
        OnPropertyChanged(nameof(SourceAccountCaption));
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

        OnPropertyChanged(nameof(NeedsNewTargetAccount));
        OnPropertyChanged(nameof(TargetAccountCaption));
    }

    /// <summary>
    /// Смена счёта списания перестраивает список зачисления: счёт, ставший счётом
    /// списания, обязан уйти из выбора, а если он там уже стоял — сброситься.
    /// Валюта операции — валюта счёта списания, и её смена сообщается: сумма
    /// осталась прежним числом, но значит уже другие деньги. Подстановка счёта
    /// при открытии формы не сообщается — прежнего счёта у неё нет. Счёт, открытый
    /// позже даты и в другой валюте, даёт два сообщения подряд намеренно: это две
    /// разные правки формы, и склеенная фраза не влезла бы во всплывающее сообщение.
    /// </summary>
    partial void OnSourceAccountChanged(AccountOption? oldValue, AccountOption? newValue)
    {
        if (newValue is { } source && TargetAccount is { } target && target.Key == source.Key)
        {
            TargetAccount = null;
        }

        FillTargetAccounts();
        KeepDateAfterOpening();

        if (oldValue is { } previous && newValue is { } current && previous.Currency != current.Currency)
        {
            Notified?.Invoke(UiTexts.TransactionCurrencyChanged);
        }
    }

    /// <summary>
    /// Счёт зачисления тоже ограничивает дату перевода.
    /// </summary>
    partial void OnTargetAccountChanged(AccountOption? value) => KeepDateAfterOpening();

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

    // Знак вида ставится только ненулевому итогу: «−0 ₽» читался бы как долг.
    // Дробная часть — только после набранной запятой: «,00» по умолчанию было бы
    // числом, которого пользователь не набирал
    private static string Hero(string expression, Currency? currency, TransactionKind kind)
    {
        if (AmountInput.HasOperation(expression))
        {
            return UiTexts.TransactionPressEquals;
        }

        decimal value = 0m;

        if (expression.Length > 0 && !AmountExpression.TryEvaluate(expression, out value))
        {
            return string.Empty;
        }

        decimal signed = kind is TransactionKind.Expense && value > 0m ? -value : value;
        string shown = currency is { } known
            ? Money.Restore(signed, known).DisplayTyped(expression)
            : MoneyFormat.Typed(signed, expression);

        return kind is TransactionKind.Income && value > 0m ? $"+{shown}" : shown;
    }

    // Незакрытое действие тона не получает: итог тогда скрыт, на его месте подсказка
    private static AmountTone Tone(string expression, TransactionKind kind) =>
        !AmountExpression.TryEvaluate(expression, out decimal value) || value is 0m
            ? AmountTone.Placeholder
        : kind switch
        {
            TransactionKind.Expense => AmountTone.Expense,
            TransactionKind.Income => AmountTone.Income,
            _ => AmountTone.Plain,
        };
}
