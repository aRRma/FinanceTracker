using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Settings.DefaultAccounts;

/// <summary>
/// Экран счёта по умолчанию: незаблокированные счета, галочка — у того, что подставит форма операции.
/// Строки «Последний использованный» нет: счёт по умолчанию есть всегда, пока есть незаблокированный счёт.
/// </summary>
public sealed partial class DefaultAccountViewModel : ScreenViewModel
{
    private readonly IAccountsQuery _accounts;
    private readonly ILocalSettings _settings;
    private readonly IChangeDefaultAccountHandler _change;

    private IReadOnlyList<AccountListItem> _open = [];
    private Guid? _selected;
    private int _generation;

    /// <summary>
    /// Создаёт модель представления экрана счёта по умолчанию.
    /// </summary>
    /// <param name="accounts">Список счетов.</param>
    /// <param name="settings">Локальные настройки: явный выбор.</param>
    /// <param name="change">Выбор счёта по умолчанию.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public DefaultAccountViewModel(
        IAccountsQuery accounts,
        ILocalSettings settings,
        IChangeDefaultAccountHandler change,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(change);

        _accounts = accounts;
        _settings = settings;
        _change = change;
    }

    /// <summary>
    /// Незаблокированные счета в порядке списка «Счета».
    /// </summary>
    public ObservableCollection<DefaultAccountOption> Accounts { get; } = [];

    /// <summary>
    /// Список прочитан хотя бы раз: до чтения пустой список значит «ещё не читали»,
    /// и объяснение про пустоту мигнуло бы на каждом заходе.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Выбирать не из чего — незаблокированных счетов нет.
    /// </summary>
    public bool IsEmpty => IsLoaded && _open.Count is 0;

    /// <summary>
    /// Перечитывает счета и выбор.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        int generation = ++_generation;

        // Два чтения независимы и идут разом: у каждого свой контекст.
        // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
        // коллекция, а её правка вне потока интерфейса роняет разметку
        Task<IReadOnlyList<AccountListItem>> accountsTask = _accounts.ReadAsync(cancellationToken);
        Task<string?> choiceTask = _settings.GetAsync(SettingName.DefaultAccountKey, cancellationToken);

        await Task.WhenAll(accountsTask, choiceTask);

        if (generation != _generation)
        {
            return;
        }

        // Список — в порядке экрана «Счета», как и подстановка: верхний в нём и есть счёт по умолчанию
        List<AccountListItem> open = [.. DefaultAccount.InScreenOrder(await accountsTask)];
        List<OpenAccount> candidates = DefaultAccount.Candidates(open);

        _open = open;
        _selected = DefaultAccount.Resolve(candidates, DefaultAccount.Parse(await choiceTask))?.Key;

        Rebuild();

        IsLoaded = true;
    }

    /// <summary>
    /// Делает счёт счётом по умолчанию.
    /// </summary>
    /// <param name="option">Строка списка.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task SelectAsync(DefaultAccountOption option, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(option);

        // Список устарел — счёт заблокировали или удалили, пока экран был открыт:
        // выбор не записан, и экран перечитывается, чтобы строка исчезла
        if (!await _change.HandleAsync(option.Key, cancellationToken))
        {
            await LoadAsync(cancellationToken);

            return;
        }

        // Галочка переезжает сразу, не дожидаясь перечитывания по оповещению
        _selected = option.Key;

        Rebuild();
    }

    private void Rebuild()
    {
        Accounts.Clear();

        foreach (AccountListItem account in _open)
        {
            Accounts.Add(new DefaultAccountOption
            {
                Key = account.Key,
                Name = account.Name,
                Caption = $"{(account.Type is AccountType.Cash ? UiTexts.AccountTypeCash : UiTexts.AccountTypeCard)} · {account.Balance.Currency}",
                Color = account.Color,
                Icon = account.Icon,
                IsSelected = account.Key == _selected
            });
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Accounts | DataChange.Settings;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
