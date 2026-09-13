using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Feed;

/// <summary>
/// Лента операций — общая и по одному счёту, одной моделью: различаются только
/// шапка, подпись строки и то, чем заполнена пустота. Читается постранично:
/// при десятках тысяч операций поднимать ленту целиком нельзя.
/// </summary>
public sealed partial class FeedViewModel : ScreenViewModel
{
    /// <summary>Строк на страницу: экран с запасом на пару прокруток.</summary>
    private const int PageSize = 50;

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private readonly IFeedQuery _feed;
    private readonly IAccountsQuery _accounts;
    private readonly IClock _clock;

    private int _loaded;

    // Номер перечитывания: по нему дочитанная страница узнаёт, что лента
    // за время запроса была перечитана заново и её строки уже не к месту
    private int _generation;

    /// <summary>Создаёт модель представления ленты.</summary>
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

    /// <summary>Счёт, чья лента показана. Пусто — общая лента.</summary>
    public Guid? AccountKey { get; private set; }

    /// <summary>Показана лента одного счёта, а не общая.</summary>
    public bool IsAccountFeed => AccountKey is not null;

    /// <summary>Дни ленты от новых к старым.</summary>
    public ObservableCollection<FeedDay> Days { get; } = [];

    /// <summary>Название счёта — заголовок ленты счёта.</summary>
    [ObservableProperty]
    public partial string AccountName { get; private set; } = string.Empty;

    /// <summary>Баланс счёта — подзаголовок ленты счёта.</summary>
    [ObservableProperty]
    public partial string AccountBalance { get; private set; } = string.Empty;

    /// <summary>Начальный остаток — строка пустой ленты счёта, иначе непонятно, откуда взялся баланс.</summary>
    [ObservableProperty]
    public partial string OpeningBalance { get; private set; } = string.Empty;

    /// <summary>Дата открытия — подпись начального остатка.</summary>
    [ObservableProperty]
    public partial string OpenedOn { get; private set; } = string.Empty;

    /// <summary>Операций нет — показывается пустое состояние.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Операции есть — показывается список.</summary>
    public bool HasItems => !IsEmpty;

    /// <summary>За последней строкой есть ещё: прокрутка к концу дочитает следующую страницу.</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>Идёт чтение.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Перечитывает ленту с начала.</summary>
    /// <param name="accountKey">Счёт, чью ленту читать; пусто — общая лента.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(Guid? accountKey, CancellationToken cancellationToken = default)
    {
        AccountKey = accountKey;
        OnPropertyChanged(nameof(IsAccountFeed));

        int generation = ++_generation;

        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются привязанные
            // коллекции, а их правка вне потока интерфейса роняет разметку
            if (accountKey is { } key)
            {
                await ReadAccountAsync(key, cancellationToken);
            }

            FeedPage page = await _feed.ReadAsync(accountKey, skip: 0, PageSize, cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            Days.Clear();
            _loaded = 0;

            Append(page);

            IsEmpty = page.Items.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Дочитывает следующую страницу в конец ленты.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (!HasMore || IsBusy)
        {
            return;
        }

        int generation = _generation;

        IsBusy = true;

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

            Append(page);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Раскладывает строки страницы по дням. Первая строка страницы может
    /// продолжать день с прошлой страницы — тогда она встаёт в него, а не заводит
    /// вторую шапку с той же датой.
    /// </summary>
    private void Append(FeedPage page)
    {
        DateOnly today = _clock.Today;
        FeedDay? current = Days.Count > 0 ? Days[^1] : null;

        foreach (FeedItem item in page.Items)
        {
            if (current is null || current.Date != item.OccurredOn)
            {
                Money? total = page.DayTotals.TryGetValue(item.OccurredOn, out Money known) ? known : null;

                current = new FeedDay(item.OccurredOn, total, today);
                Days.Add(current);
            }

            current.Add(FeedRowItem.From(item, showAccount: !IsAccountFeed));
        }

        _loaded += page.Items.Count;
        HasMore = page.HasMore;
    }

    /// <summary>Шапка ленты счёта: название, баланс, начальный остаток с датой открытия.</summary>
    private async Task ReadAccountAsync(Guid key, CancellationToken cancellationToken)
    {
        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);

        AccountListItem? account = accounts.FirstOrDefault(candidate => candidate.Key == key);

        if (account is null)
        {
            return;
        }

        AccountName = account.Name;
        AccountBalance = account.Balance.Display;
        OpeningBalance = account.OpeningBalance.Display;
        OpenedOn = account.OpenedOn.ToString("d MMMM yyyy", Russian);
    }

    /// <inheritdoc />
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Accounts | DataChange.Categories | DataChange.Places;

    /// <inheritdoc />
    protected override void Reload() => _ = LoadAsync(AccountKey);
}
