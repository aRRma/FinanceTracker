using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Accounts.Catalog;

/// <summary>
/// Справочник счетов: три раздела — доступные к тратам, накопления и закрытые.
/// Порядок внутри первых двух задаётся перетаскиванием.
/// </summary>
public sealed partial class AccountsViewModel : ScreenViewModel
{
    private readonly IAccountsQuery _accounts;
    private readonly IReorderAccountsHandler _reorder;

    /// <summary>
    /// Создаёт модель представления справочника счетов.
    /// </summary>
    /// <param name="accounts">Список счетов с балансами.</param>
    /// <param name="reorder">Сохранение порядка счетов.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public AccountsViewModel(
        IAccountsQuery accounts,
        IReorderAccountsHandler reorder,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(reorder);

        _accounts = accounts;
        _reorder = reorder;
    }

    /// <summary>
    /// Счета, деньги которых пользователь считает тратимыми.
    /// </summary>
    public ObservableCollection<AccountRowItem> Spendable { get; } = [];

    /// <summary>
    /// Накопления — счета со «скрыть из расчётов».
    /// </summary>
    public ObservableCollection<AccountRowItem> Savings { get; } = [];

    /// <summary>
    /// Закрытые счета. Остаются в справочнике, ленте и отчёте.
    /// </summary>
    public ObservableCollection<AccountRowItem> Closed { get; } = [];

    /// <summary>
    /// В справочнике есть накопления. Заголовок раздела без единой строки под ним
    /// выглядит потерянными данными, а не пустым разделом.
    /// </summary>
    public bool HasSavings => Savings.Count > 0;

    /// <summary>
    /// В справочнике есть закрытые счета.
    /// </summary>
    public bool HasClosed => Closed.Count > 0;

    /// <summary>
    /// Идёт чтение. Запись открыта намеренно: к этому признаку привязан жест
    /// «потянуть вниз», и он сам поднимает его в начале обновления.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Перечитывает справочник.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);

            Fill(Spendable, accounts.Where(static account => !account.IsClosed && !account.ExcludedFromTotals));
            Fill(Savings, accounts.Where(static account => !account.IsClosed && account.ExcludedFromTotals));
            Fill(Closed, accounts.Where(static account => account.IsClosed));

            OnPropertyChanged(nameof(HasSavings));
            OnPropertyChanged(nameof(HasClosed));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Сохраняет порядок после перетаскивания. Список подаётся целиком и в том
    /// виде, в каком он на экране: пользователь видит именно его.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public Task ReorderAsync(CancellationToken cancellationToken = default)
    {
        Guid[] keys =
        [
            .. Spendable.Select(static account => account.Key),
            .. Savings.Select(static account => account.Key),
            .. Closed.Select(static account => account.Key)
        ];

        return _reorder.HandleAsync(keys, cancellationToken);
    }

    /// <summary>
    /// Переставляет перетащенный счёт на место другого и сохраняет порядок.
    /// Переезд между разделами не выполняется: раздел — это признак счёта,
    /// а не позиция в списке, и менять его перетаскиванием значило бы молча
    /// снимать «скрыть из расчётов».
    /// </summary>
    /// <param name="dragged">Счёт, который тащили.</param>
    /// <param name="target">Счёт, на который его бросили.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public Task MoveAsync(AccountRowItem dragged, AccountRowItem target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dragged);
        ArgumentNullException.ThrowIfNull(target);

        ObservableCollection<AccountRowItem>? section = SectionOf(dragged);

        if (section is null || dragged == target || SectionOf(target) != section)
        {
            return Task.CompletedTask;
        }

        section.Move(section.IndexOf(dragged), section.IndexOf(target));

        return ReorderAsync(cancellationToken);
    }

    private ObservableCollection<AccountRowItem>? SectionOf(AccountRowItem account)
    {
        if (Spendable.Contains(account))
        {
            return Spendable;
        }

        if (Savings.Contains(account))
        {
            return Savings;
        }

        return Closed.Contains(account) ? Closed : null;
    }

    private static void Fill(ObservableCollection<AccountRowItem> target, IEnumerable<AccountListItem> accounts)
    {
        target.Clear();

        foreach (AccountListItem account in accounts)
        {
            target.Add(AccountRowItem.From(account));
        }
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Accounts | DataChange.Transactions;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
