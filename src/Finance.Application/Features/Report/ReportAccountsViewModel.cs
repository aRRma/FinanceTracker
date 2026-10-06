using System.Collections.ObjectModel;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Report;

/// <summary>
/// Выбор счетов отчёта: все неудалённые счета по валютам, отметки и «Все активные»
/// у каждой валюты. Выбор применяется сразу, без «Готово»: отчёт за экраном
/// перечитается, когда на него вернутся.
/// </summary>
/// <remarks>
/// Отметки на экране и выбор отчёта — не одно и то же. Снять все отметки — законный
/// шаг перехода на другую валюту, а отчёт по пустому набору бессмыслен: экран
/// остаётся без отметок, а отчёт тем временем считается по умолчанию.
/// </remarks>
public sealed class ReportAccountsViewModel
{
    private readonly IReportAccountsQuery _accounts;
    private readonly ReportChoice _choice;

    /// <summary>
    /// Создаёт модель представления выбора счетов отчёта.
    /// </summary>
    /// <param name="accounts">Счета для отчёта.</param>
    /// <param name="choice">Выбор счетов отчёта, общий с отчётом.</param>
    public ReportAccountsViewModel(IReportAccountsQuery accounts, ReportChoice choice)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(choice);

        _accounts = accounts;
        _choice = choice;
    }

    /// <summary>
    /// Отказ, который страница показывает всплывающим сообщением.
    /// </summary>
    public event Action<string>? Notified;

    /// <summary>
    /// Счета по валютам.
    /// </summary>
    public ObservableCollection<ReportAccountsSection> Sections { get; } = [];

    /// <summary>
    /// Читает счета и ставит отметки по нынешнему выбору.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняются
        // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
        IReadOnlyList<ReportAccount> all = await _accounts.ReadAsync(cancellationToken);
        ReportAccounts chosen = _choice.Accounts;

        Sections.Clear();

        foreach (IGrouping<Currency, ReportAccount> group in all.GroupBy(static account => account.Currency))
        {
            Sections.Add(new ReportAccountsSection(
                group.Key,
                group.Select(account => new ReportAccountRow(account, chosen.Includes(account)))));
        }

        MarkSections();
    }

    /// <summary>
    /// Ставит или снимает отметку. Счёт другой валюты, чем уже отмеченные, не
    /// отмечается: отчёт считается в одной валюте, и об этом говорит сообщение.
    /// </summary>
    /// <param name="row">Строка счёта.</param>
    public void Toggle(ReportAccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsChecked && CheckedSection() is { } checkedSection && checkedSection.Currency != row.Currency)
        {
            Notified?.Invoke(UiTexts.ReportAccountsOneCurrency);
            return;
        }

        row.IsChecked = !row.IsChecked;
        Commit();
    }

    /// <summary>
    /// Заменяет выбор всеми активными счетами валюты раздела. Отметки других валют
    /// снимаются: так переход на другую валюту — одно касание.
    /// </summary>
    /// <param name="section">Раздел валюты.</param>
    public void ChooseAllActive(ReportAccountsSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        foreach (ReportAccountsSection each in Sections)
        {
            foreach (ReportAccountRow row in each)
            {
                row.IsChecked = each == section && row.IsActive;
            }
        }

        Commit();
    }

    /// <summary>
    /// Переводит отметки в выбор отчёта. Пустые отметки — умолчание; ровно активные
    /// счета валюты — «все активные» правилом, а не снимком: тогда и новый счёт
    /// этой валюты войдёт в отчёт сам, а подсветка не загорится зря на обычном отчёте.
    /// </summary>
    private void Commit()
    {
        ReportAccountsSection? section = CheckedSection();

        _choice.Accounts = section is null ? ReportAccounts.Default
            : section.CheckedAreActive() ? ReportAccounts.AllActive(section.Currency)
            : ReportAccounts.Chosen(section.Currency, section.Where(static row => row.IsChecked).Select(static row => row.Key));

        MarkSections();
    }

    private void MarkSections()
    {
        foreach (ReportAccountsSection section in Sections)
        {
            section.IsAllActive = section.CheckedAreActive();
        }
    }

    // Отметки бывают только в одном разделе: чужая валюта отвергается при отметке
    private ReportAccountsSection? CheckedSection()
    {
        foreach (ReportAccountsSection section in Sections)
        {
            if (section.HasChecked)
            {
                return section;
            }
        }

        return null;
    }
}
