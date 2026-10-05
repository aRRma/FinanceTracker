using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Deletion;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Values;

namespace Finance.Application.Features.Feed;

/// <summary>
/// Лента операций — общая и по одному счёту, одной моделью: различаются только
/// шапка, подпись строки и то, чем заполнена пустота. Читается постранично:
/// при десятках тысяч операций поднимать ленту целиком нельзя. Из ленты операции
/// удаляют — смахиванием по одной или выделением по нескольку.
/// </summary>
public sealed partial class FeedViewModel : ScreenViewModel, ISelectionModel
{
    /// <summary>
    /// Строк на страницу: экран с запасом на пару прокруток.
    /// </summary>
    private const int PageSize = 50;

    private readonly IFeedQuery _feed;
    private readonly IAccountsQuery _accounts;
    private readonly IDeleteTransactionsHandler _delete;
    private readonly ITransactionDeletionQuery _deletion;
    private readonly IClock _clock;

    // Выделенные операции — ключами, отдельно от строк: строки при перечитывании
    // создаются заново, а отметки обязаны пережить его у оставшихся
    private readonly HashSet<Guid> _selected = [];

    private int _loaded;

    // Последняя прочитанная строка: с неё дочитывается следующая страница
    private FeedCursor? _next;

    // Номер перечитывания: по нему дочитанная страница узнаёт, что лента
    // за время запроса была перечитана заново и её строки уже не к месту
    private int _generation;

    // Идёт чтение: дочитывание ждёт, пока лента не прочитана целиком
    // и пока не пришла предыдущая страница
    private bool _reading;

    // День, которым подписаны шапки: после полуночи и смены часового пояса
    // «сегодня» другое, и прочитанная лента устаревает без единой правки
    private DateOnly _shownOn;

    /// <summary>
    /// Создаёт модель представления ленты.
    /// </summary>
    /// <param name="feed">Чтение ленты.</param>
    /// <param name="accounts">Счета с балансами — для шапки ленты счёта.</param>
    /// <param name="delete">Удаление операций.</param>
    /// <param name="deletion">Последствия удаления — для текста подтверждения.</param>
    /// <param name="clock">Часы: «сегодня» пользователя для шапок дней.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public FeedViewModel(
        IFeedQuery feed,
        IAccountsQuery accounts,
        IDeleteTransactionsHandler delete,
        ITransactionDeletionQuery deletion,
        IClock clock,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(deletion);
        ArgumentNullException.ThrowIfNull(clock);

        _feed = feed;
        _accounts = accounts;
        _delete = delete;
        _deletion = deletion;
        _clock = clock;
    }

    /// <summary>
    /// Счёт, чья лента показана. Пусто — общая лента.
    /// </summary>
    public Guid? AccountKey { get; private set; }

    /// <summary>
    /// Показана лента одного счёта, а не общая.
    /// </summary>
    public bool IsAccountFeed => AccountKey is not null;

    /// <summary>
    /// Дни ленты от новых к старым.
    /// </summary>
    public ObservableCollection<FeedDay> Days { get; } = [];

    /// <summary>
    /// Название счёта — заголовок ленты счёта.
    /// </summary>
    [ObservableProperty]
    public partial string AccountName { get; private set; } = string.Empty;

    /// <summary>
    /// Знак счёта — плашка рядом с названием в шапке ленты счёта. В строках ленты
    /// счёта жетонов нет: счёт один, и его знак — здесь.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccountMark))]
    public partial AccountMark? AccountMark { get; private set; }

    /// <summary>
    /// Счёт прочитан — его знак есть что показать.
    /// </summary>
    public bool HasAccountMark => AccountMark is not null;

    /// <summary>
    /// Баланс счёта — подзаголовок ленты счёта.
    /// </summary>
    [ObservableProperty]
    public partial string AccountBalance { get; private set; } = string.Empty;

    /// <summary>
    /// Баланс счёта отрицателен: в шапке ленты его показывают смысловым цветом.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAccountBalanceNegative { get; private set; }

    /// <summary>
    /// Начальный остаток — строка пустой ленты счёта, иначе непонятно, откуда взялся баланс.
    /// </summary>
    [ObservableProperty]
    public partial string OpeningBalance { get; private set; } = string.Empty;

    /// <summary>
    /// Дата открытия — подпись начального остатка.
    /// </summary>
    [ObservableProperty]
    public partial string OpenedOn { get; private set; } = string.Empty;

    /// <summary>
    /// Операций нет — показывается пустое состояние.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Лента прочитана хотя бы раз. До этого сказать «операций нет» нельзя:
    /// пустое состояние мигнуло бы и сменилось списком.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Операции есть — показывается список.
    /// </summary>
    public bool HasItems => IsLoaded && !IsEmpty;

    /// <summary>
    /// За последней строкой есть ещё: прокрутка к концу дочитает следующую страницу.
    /// </summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>
    /// Включено выделение: касание строки отмечает её, а не открывает.
    /// Включается долгим нажатием, выключается «назад», крестиком и удалением.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelecting { get; private set; }

    /// <summary>
    /// Сколько строк выделено — подпись панели выделения.
    /// </summary>
    [ObservableProperty]
    public partial string SelectionTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Лента устарела: пока её не было видно, изменились операции или справочники,
    /// или наступил другой день. Неустаревшую ленту на возврате не перечитывают:
    /// перечитывание вернуло бы прокрутку к началу, и заглянувший в операцию
    /// из глубины истории терял бы место, с которого ушёл.
    /// </summary>
    public override bool IsOutdated => base.IsOutdated || _shownOn != _clock.Today;

    /// <summary>
    /// Перечитывает ленту с начала. Ту же ленту — на прочитанную глубину, а не на
    /// одну страницу: правка операции из глубины истории иначе срезала бы всё
    /// дочитанное, и возвращаться к месту пришлось бы заново.
    /// </summary>
    /// <param name="accountKey">Счёт, чью ленту читать; пусто — общая лента.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(Guid? accountKey, CancellationToken cancellationToken = default)
    {
        int take = IsLoaded && accountKey == AccountKey ? Math.Max(PageSize, _loaded) : PageSize;

        AccountKey = accountKey;
        OnPropertyChanged(nameof(IsAccountFeed));

        int generation = ++_generation;

        _reading = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются привязанные
            // коллекции, а их правка вне потока интерфейса роняет разметку
            if (accountKey is { } key)
            {
                await ReadAccountAsync(key, cancellationToken);
            }

            DateOnly today = _clock.Today;
            FeedPage page = await _feed.ReadAsync(accountKey, after: null, take, cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            _loaded = 0;
            _shownOn = today;

            List<FeedDay> fresh = [];
            Append(page, fresh);
            Replace(fresh);
            ForgetVanished();

            IsEmpty = page.Items.Count == 0;
            IsLoaded = true;
        }
        finally
        {
            // Чтение снимает отметку только последнее: более раннее, завершившись,
            // открыло бы дочитывание, пока свежее ещё наполняет ленту
            if (generation == _generation)
            {
                _reading = false;
            }
        }
    }

    /// <summary>
    /// Дочитывает следующую страницу в конец ленты.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (!HasMore || _reading)
        {
            return;
        }

        int generation = _generation;

        _reading = true;

        try
        {
            FeedPage page = await _feed.ReadAsync(AccountKey, _next, PageSize, cancellationToken);

            // Пока страница читалась, лента могла быть перечитана с начала:
            // её строки уже показаны, и дописывать их значило бы показать
            // те же операции дважды
            if (generation != _generation)
            {
                return;
            }

            Append(page, Days);
        }
        finally
        {
            // Отметку снимает только последнее чтение: более раннее, завершившись
            // позже перечитывания с начала, сняло бы отметку свежего запроса
            if (generation == _generation)
            {
                _reading = false;
            }
        }
    }

    /// <summary>
    /// Долгое нажатие: включает выделение и отмечает строку, на которой оно было.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    public void StartSelection(Guid key)
    {
        IsSelecting = true;

        if (_selected.Add(key))
        {
            Mark(key, selected: true);
        }

        ShowCount();
    }

    /// <summary>
    /// Касание строки при выделении: отмечает её или снимает отметку. Снята последняя —
    /// выделение выключается само: пустое выделение ничего не умеет, а касание
    /// следующей строки иначе отметило бы её, вместо того чтобы открыть.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    public void ToggleSelection(Guid key)
    {
        if (!IsSelecting)
        {
            return;
        }

        bool selected = _selected.Add(key);

        if (!selected)
        {
            _selected.Remove(key);
        }

        Mark(key, selected);

        if (_selected.Count == 0)
        {
            EndSelection();
        }
        else
        {
            ShowCount();
        }
    }

    /// <inheritdoc />
    public void EndSelection()
    {
        // Один проход по строкам, а не поиск каждой отмеченной: снятие идёт
        // на каждом уходе со страницы, а прочитанных строк бывают тысячи
        if (_selected.Count > 0)
        {
            foreach (FeedDay day in Days)
            {
                for (int i = 0; i < day.Count; i++)
                {
                    if (day[i].IsSelected)
                    {
                        day[i] = day[i] with { IsSelected = false };
                    }
                }
            }
        }

        ForgetSelection();
    }

    private void ForgetSelection()
    {
        _selected.Clear();
        IsSelecting = false;
        SelectionTitle = string.Empty;
    }

    /// <summary>
    /// Последствия удаления одной операции — для подтверждения смахивания.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public Task<TransactionDeletion> DeletePromptAsync(Guid key, CancellationToken cancellationToken = default) =>
        _deletion.ReadAsync([key], cancellationToken);

    /// <summary>
    /// Последствия удаления выделенного — для подтверждения.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public Task<TransactionDeletion> DeleteSelectedPromptAsync(CancellationToken cancellationToken = default) =>
        _deletion.ReadAsync([.. _selected], cancellationToken);

    /// <summary>
    /// Удаляет одну операцию. Подтверждение уже получено экраном.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public Task DeleteAsync(Guid key, CancellationToken cancellationToken = default) =>
        _delete.HandleAsync([key], cancellationToken);

    /// <summary>
    /// Удаляет выделенное и выключает выделение. Подтверждение уже получено экраном.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task DeleteSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (_selected.Count == 0)
        {
            return;
        }

        await _delete.HandleAsync([.. _selected], cancellationToken);

        // После записи, а не до: не удалось — отметки остаются, и повторить
        // можно, не выделяя заново. Строки не перекрашиваются: перечитывание
        // по оповещению о записи их уберёт, а замены строк вперемешку с заменой
        // их дня роняли сгруппированный список рассинхроном позиций
        ForgetSelection();
    }

    /// <summary>
    /// Ставит в день строку с новой отметкой на место прежней. Строку целиком, а не
    /// признак у неё: заменённая строка приходит в список одной заменой, и перерисовывается
    /// только она.
    /// </summary>
    private void Mark(Guid key, bool selected)
    {
        foreach (FeedDay day in Days)
        {
            for (int i = 0; i < day.Count; i++)
            {
                if (day[i].Key == key)
                {
                    if (day[i].IsSelected != selected)
                    {
                        day[i] = day[i] with { IsSelected = selected };
                    }

                    return;
                }
            }
        }
    }

    /// <summary>
    /// После перечитывания забывает отметки строк, которых в ленте больше нет:
    /// их удалили или перенесли на другой счёт, и удалять их отсюда было бы
    /// удалением невидимого.
    /// </summary>
    private void ForgetVanished()
    {
        if (_selected.Count == 0)
        {
            return;
        }

        HashSet<Guid> shown = [];

        foreach (FeedDay day in Days)
        {
            foreach (FeedRowItem row in day)
            {
                shown.Add(row.Key);
            }
        }

        _selected.IntersectWith(shown);

        if (_selected.Count == 0)
        {
            EndSelection();
        }
        else
        {
            ShowCount();
        }
    }

    private void ShowCount() =>
        SelectionTitle = string.Format(UiCulture.Current, UiTexts.FeedSelectedCount, _selected.Count);

    /// <summary>
    /// Раскладывает строки страницы по дням. Первая строка страницы может
    /// продолжать день с прошлой страницы — тогда она встаёт в него, а не заводит
    /// вторую шапку с той же датой.
    /// </summary>
    private void Append(FeedPage page, IList<FeedDay> days)
    {
        DateOnly today = _clock.Today;
        FeedDay? current = days.Count > 0 ? days[^1] : null;

        foreach (FeedItem item in page.Items)
        {
            if (current is null || current.Date != item.OccurredOn)
            {
                Money? total = page.DayTotals.TryGetValue(item.OccurredOn, out Money known) ? known : null;

                current = new FeedDay(item.OccurredOn, total, today);
                days.Add(current);
            }

            FeedRowItem row = FeedRowItem.From(item, showAccount: !IsAccountFeed);

            current.Add(_selected.Contains(row.Key) ? row with { IsSelected = true } : row);
        }

        _loaded += page.Items.Count;
        _next = page.Next;
        HasMore = page.HasMore;
    }

    /// <summary>
    /// Ставит перечитанные дни на место прежних поштучно, меняя только изменившиеся.
    /// Очистка с наполнением заново приходит в список сбросом, и тот возвращает
    /// прокрутку к началу: сохранивший правку из глубины истории оказывался бы наверху.
    /// </summary>
    /// <remarks>
    /// Дни сводятся по дате, а не по месту в списке: исчезнувший или новый день
    /// сдвигает все дни под собой, и сверка по месту меняла бы каждый из них —
    /// удаление единственной операции дня перерисовывало бы всю дочитанную ленту.
    /// </remarks>
    private void Replace(List<FeedDay> fresh)
    {
        // Опустевшая лента очищается сбросом, а не поштучно: убранный поштучно
        // последний день оставлял список пустым без заглушки «операций нет».
        // Прокрутку беречь в пустом списке незачем
        if (fresh.Count == 0)
        {
            Days.Clear();

            return;
        }

        int i = 0;

        // Обе последовательности идут от новых дней к старым: показанный день
        // новее перечитанного — значит, его больше нет
        foreach (FeedDay day in fresh)
        {
            while (i < Days.Count && Days[i].Date > day.Date)
            {
                Days.RemoveAt(i);
            }

            if (i < Days.Count && Days[i].Date == day.Date)
            {
                if (day.Count != Days[i].Count)
                {
                    // Замена дня с другим числом строк приходит в сгруппированный список
                    // одним изменённым элементом, и тот падал с рассинхроном позиций,
                    // когда из дня удаляли несколько строк. Удаление со вставкой он
                    // понимает однозначно, а прокрутку они не сбрасывают
                    Days.RemoveAt(i);
                    Days.Insert(i, day);
                }
                else if (!day.SameAs(Days[i]))
                {
                    Days[i] = day;
                }
            }
            else
            {
                Days.Insert(i, day);
            }

            i++;
        }

        while (Days.Count > i)
        {
            Days.RemoveAt(Days.Count - 1);
        }
    }

    /// <summary>
    /// Шапка ленты счёта: название, баланс, начальный остаток с датой открытия.
    /// </summary>
    private async Task ReadAccountAsync(Guid key, CancellationToken cancellationToken)
    {
        // Один счёт, а не весь список: полный список считал бы балансы всех счетов
        // ради одной шапки, и лента счёта дорожала бы вместе со всей историей
        AccountListItem? account = await _accounts.ReadOneAsync(key, cancellationToken);

        if (account is null)
        {
            return;
        }

        AccountName = account.Name;
        AccountMark = new AccountMark(account.Name, account.Color, account.Icon);
        AccountBalance = account.Balance.Display;
        IsAccountBalanceNegative = account.Balance.IsNegative;
        OpeningBalance = account.OpeningBalance.Display;
        OpenedOn = DateText.DayWithYear(account.OpenedOn);
    }

    /// <inheritdoc />
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Accounts | DataChange.Categories | DataChange.Places;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync(AccountKey);
}
