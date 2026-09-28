using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Values;

namespace Finance.Application.Features.Feed;

/// <summary>
/// Лента операций — общая и по одному счёту, одной моделью: различаются только
/// шапка, подпись строки и то, чем заполнена пустота. Читается постранично:
/// при десятках тысяч операций поднимать ленту целиком нельзя.
/// </summary>
public sealed partial class FeedViewModel : ScreenViewModel
{
    /// <summary>
    /// Строк на страницу: экран с запасом на пару прокруток.
    /// </summary>
    private const int PageSize = 50;

    private readonly IFeedQuery _feed;
    private readonly IAccountsQuery _accounts;
    private readonly IClock _clock;

    private int _loaded;

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
    /// <param name="clock">Часы: «сегодня» пользователя для шапок дней.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public FeedViewModel(IFeedQuery feed, IAccountsQuery accounts, IClock clock, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(clock);

        _feed = feed;
        _accounts = accounts;
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
            FeedPage page = await _feed.ReadAsync(accountKey, skip: 0, take, cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            _loaded = 0;
            _shownOn = today;

            List<FeedDay> fresh = [];
            Append(page, fresh);
            Replace(fresh);

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
            FeedPage page = await _feed.ReadAsync(AccountKey, _loaded, PageSize, cancellationToken);

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

            current.Add(FeedRowItem.From(item, showAccount: !IsAccountFeed));
        }

        _loaded += page.Items.Count;
        HasMore = page.HasMore;
    }

    /// <summary>
    /// Ставит перечитанные дни на место прежних поштучно, меняя только изменившиеся.
    /// Очистка с наполнением заново приходит в список сбросом, и тот возвращает
    /// прокрутку к началу: сохранивший правку из глубины истории оказывался бы наверху.
    /// </summary>
    private void Replace(List<FeedDay> fresh)
    {
        for (int i = 0; i < fresh.Count; i++)
        {
            if (i >= Days.Count)
            {
                Days.Add(fresh[i]);
            }
            else if (!fresh[i].SameAs(Days[i]))
            {
                Days[i] = fresh[i];
            }
        }

        while (Days.Count > fresh.Count)
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
