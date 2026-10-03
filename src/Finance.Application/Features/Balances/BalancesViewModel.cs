using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Features.Balances;

/// <summary>
/// Главный экран: счета по валютам и «доступно к тратам» в каждой. Единой суммы
/// по всем валютам нет и не будет — складывать рубли с евро не по какому курсу.
/// </summary>
public sealed partial class BalancesViewModel : ScreenViewModel
{
    private readonly IAccountsQuery _accounts;

    // Номер чтения: жест обновления и перечитывание по чужой правке могут
    // совпасть, и отставшее чтение не должно перекрыть свежее
    private int _generation;

    /// <summary>
    /// Создаёт модель представления главного экрана.
    /// </summary>
    /// <param name="accounts">Список счетов с балансами.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public BalancesViewModel(IAccountsQuery accounts, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        _accounts = accounts;
    }

    /// <summary>
    /// Разделы по валютам, в порядке появления счетов.
    /// </summary>
    public ObservableCollection<CurrencySection> Sections { get; } = [];

    /// <summary>
    /// Счета прочитаны хотя бы раз. До этого экран не знает, пусто на нём или нет,
    /// и не имеет права ни показать приглашение завести счёт, ни предложить
    /// записать операцию: и то и другое мигнуло бы и сменилось на противоположное.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccounts))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Счетов нет вовсе: главный экран предлагает завести первый вместо списка,
    /// а добавление операции недоступно — записывать её некуда.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccounts))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Счета есть — показывается список, а не приглашение завести первый.
    /// </summary>
    public bool HasAccounts => IsLoaded && !IsEmpty;

    /// <summary>
    /// Перечитывает счета и балансы.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        int generation = ++_generation;

        // ConfigureAwait(false) здесь недопустим, хотя и стоит в остальном
        // прикладном слое: дальше наполняются привязанные коллекции,
        // а их правка вне потока интерфейса роняет разметку
        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);

        if (generation != _generation)
        {
            return;
        }

        // Заблокированный счёт с главного экрана уходит целиком: он выведен
        // из употребления, а лента и отчёт его сохраняют
        AccountListItem[] visible = accounts.Where(static account => !account.IsClosed).ToArray();

        Sections.Clear();

        foreach (CurrencySection section in BuildSections(visible))
        {
            Sections.Add(section);
        }

        // Пусто — когда счетов нет вовсе, а не когда все они заблокированы:
        // иначе блокировка последнего счёта выглядела бы как первый запуск
        IsEmpty = accounts.Count == 0;
        IsLoaded = true;
    }

    /// <summary>
    /// Разделы по валютам: сначала те, где счета появились раньше.
    /// </summary>
    private static IEnumerable<CurrencySection> BuildSections(IReadOnlyList<AccountListItem> accounts)
    {
        foreach (IGrouping<Currency, AccountListItem> group in accounts.GroupBy(static account => account.Balance.Currency))
        {
            AccountListItem[] spendable = group.Where(static account => !account.ExcludedFromTotals).ToArray();

            Money available = Money.Zero(group.Key);

            foreach (AccountListItem account in spendable)
            {
                available += account.Balance;
            }

            yield return new CurrencySection(
                group.Key.SectionTitle,
                available.Display,
                available.IsNegative,
                [.. spendable.Select(AccountTile.From)],
                [.. group.Where(static account => account.ExcludedFromTotals).Select(AccountTile.From)]);
        }
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Accounts | DataChange.Transactions;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
