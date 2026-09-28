using Finance.Application.Texts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Выбор счёта для формы операции (экран B-02). Балансы показаны прямо в списке:
/// выбирая счёт для расхода, обычно и хотят знать, хватит ли на нём денег.
/// Тот же экран выбирает счёт зачисления перевода — тогда счёт списания из списка
/// исключён: перевод на себя запрещён доменом, и предлагать его значило бы
/// рассказывать о запрете уже после сохранения.
/// </summary>
public sealed partial class AccountPickerViewModel : ObservableObject
{
    private readonly IAccountsQuery _accounts;
    private readonly TransactionPicks _picks;

    private bool _forTarget;

    /// <summary>
    /// Создаёт модель представления выбора счёта.
    /// </summary>
    /// <param name="accounts">Список счетов с балансами.</param>
    /// <param name="picks">Куда кладётся выбор для формы операции.</param>
    public AccountPickerViewModel(IAccountsQuery accounts, TransactionPicks picks)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(picks);

        _accounts = accounts;
        _picks = picks;
    }

    /// <summary>
    /// Счета по валютам: заголовок раздела — валюта.
    /// </summary>
    public ObservableCollection<AccountPickerSection> Sections { get; } = [];

    /// <summary>
    /// Заголовок экрана: у перевода выбирают не просто счёт, а сторону.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = UiTexts.PickAccountTitle;

    /// <summary>
    /// Читает счета к выбору: закрытые не предлагаются — записать на них нечего.
    /// </summary>
    /// <param name="selected">Счёт, стоящий в форме сейчас, — он помечен галочкой.</param>
    /// <param name="excluded">Счёт, которого в списке быть не должно: списание при выборе «Куда».</param>
    /// <param name="forTarget">Выбирается счёт зачисления перевода.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(
        Guid? selected,
        Guid? excluded,
        bool forTarget,
        CancellationToken cancellationToken = default)
    {
        _forTarget = forTarget;
        Title = forTarget ? UiTexts.PickAccountTarget : UiTexts.PickAccountSource;

        // ConfigureAwait(false) здесь недопустим: следом наполняются
        // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);

        Sections.Clear();

        IEnumerable<AccountListItem> offered = accounts
            .Where(account => !account.IsClosed && account.Key != excluded);

        foreach (IGrouping<Currency, AccountListItem> group in offered.GroupBy(account => account.Balance.Currency))
        {
            // Накопления в конце раздела: их выбирают редко, а сверху ждут то,
            // чем платят каждый день
            IEnumerable<AccountPickerRow> rows = group
                .OrderBy(static account => account.ExcludedFromTotals)
                .ThenBy(static account => account.SortOrder)
                .Select(account => Row(account, selected));

            Sections.Add(new AccountPickerSection(group.Key.SectionTitle, rows));
        }
    }

    /// <summary>
    /// Запоминает выбор: форма заберёт его, когда вернётся на экран.
    /// </summary>
    /// <param name="row">Выбранная строка.</param>
    [RelayCommand]
    public void Pick(AccountPickerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_forTarget)
        {
            _picks.TargetAccount = row.Key;
        }
        else
        {
            _picks.Account = row.Key;
        }
    }

    private static AccountPickerRow Row(AccountListItem account, Guid? selected) => new(
        account.Key,
        AccountIcon.For(account.Type, account.ExcludedFromTotals),
        account.Name,
        account.ExcludedFromTotals ? UiTexts.PickAccountSavings : string.Empty,
        account.Balance.Display,
        account.Balance.IsNegative,
        account.Key == selected);
}
