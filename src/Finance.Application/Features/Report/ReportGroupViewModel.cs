using Finance.Application.Texts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Features.Report;

/// <summary>
/// Второй уровень отчёта: подкатегории одной группы за месяц. Доли пересчитаны
/// внутри группы, а не от итога месяца: иначе все числа были бы мелкими
/// и несравнимыми между собой.
/// </summary>
public sealed partial class ReportGroupViewModel : ScreenViewModel
{
    private readonly IReportQuery _report;
    private readonly IReportAccountsQuery _accounts;
    private readonly ReportChoice _choice;

    private Guid _groupKey;
    private int _generation;

    /// <summary>
    /// Создаёт модель представления группы отчёта.
    /// </summary>
    /// <param name="report">Суммы отчёта.</param>
    /// <param name="accounts">Счета — знаки набора в подписи.</param>
    /// <param name="choice">Выбор счетов отчёта.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ReportGroupViewModel(
        IReportQuery report,
        IReportAccountsQuery accounts,
        ReportChoice choice,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(choice);

        _report = report;
        _accounts = accounts;
        _choice = choice;
    }

    /// <summary>
    /// Знаки счетов набора справа в подписи — только при своём наборе: иначе здесь
    /// не видно, что суммы посчитаны не как обычно.
    /// </summary>
    [ObservableProperty]
    public partial ReportAccountsLine Accounts { get; private set; } = ReportAccountsLine.Empty;

    /// <summary>
    /// Подкатегории группы, по убыванию суммы.
    /// </summary>
    public ObservableCollection<ReportRowItem> Rows { get; } = [];

    /// <summary>
    /// Месяц отчёта — приходит параметром перехода, здесь не переключается.
    /// </summary>
    public ReportMonth Month { get; private set; }

    /// <summary>
    /// Название группы — заголовок экрана.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Подпись под заголовком: «август 2026 · 37% расходов».
    /// </summary>
    [ObservableProperty]
    public partial string Caption { get; private set; } = string.Empty;

    /// <summary>
    /// Сумма группы за месяц со знаком.
    /// </summary>
    [ObservableProperty]
    public partial string Total { get; private set; } = string.Empty;

    /// <summary>
    /// Итог — расходный: красится тем же цветом, что строки под ним.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTotalExpense { get; private set; }

    /// <summary>
    /// Группа за месяц пуста: операции переехали или удалены после перехода сюда.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Уровень прочитан хотя бы раз. До этого сказать «операций группы нет» нельзя:
    /// пустое состояние мигнуло бы и сменилось списком подкатегорий.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Есть что показать: список и итог видны.
    /// </summary>
    public bool HasItems => IsLoaded && !IsEmpty;

    /// <summary>
    /// Читает группу за месяц. Первый уровень читается ради шапки: название, вид
    /// и доля группы в месяце. Протащить долю параметром перехода нельзя — после
    /// правки операции на третьем уровне она осталась бы прежней, а числа под ней изменились.
    /// </summary>
    /// <param name="groupKey">Ключ группы.</param>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(Guid groupKey, ReportMonth month, CancellationToken cancellationToken = default)
    {
        _groupKey = groupKey;
        Month = month;

        // Номер чтения: перечитывание по чужой правке может обогнать первое чтение,
        // и строки более раннего запроса легли бы поверх более свежих
        int generation = ++_generation;
        ReportAccounts accounts = _choice.Accounts;

        // Чтения независимы и идут разом: у каждого свой контекст,
        // а последовательно экран ждал бы сумму обращений к базе.
        // Счета нужны только знакам своего набора — при обычном их не читают
        Task<IReadOnlyList<ReportTotal>> groupsTask = _report.ReadGroupsAsync(month, accounts, cancellationToken);
        Task<IReadOnlyList<ReportTotal>> rowsTask = _report.ReadSubcategoriesAsync(groupKey, month, accounts, cancellationToken);
        Task<IReadOnlyList<ReportAccount>> listTask = ReportAccountsLine.ReadForTokensAsync(_accounts, accounts, cancellationToken);

        // ConfigureAwait(false) здесь недопустим: дальше наполняются
        // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
        await Task.WhenAll(groupsTask, rowsTask, listTask);

        IReadOnlyList<ReportTotal> groups = await groupsTask;
        IReadOnlyList<ReportTotal> rows = await rowsTask;
        IReadOnlyList<ReportAccount> list = await listTask;

        if (generation != _generation)
        {
            return;
        }

        Accounts = ReportAccountsLine.Of(accounts, list);
        Rebuild(groups, rows, accounts.Currency);
    }

    private void Rebuild(IReadOnlyList<ReportTotal> groups, IReadOnlyList<ReportTotal> rows, Currency currency)
    {
        Rows.Clear();

        ReportTotal? self = null;
        Money monthTotal = Money.Zero(currency);

        foreach (ReportTotal group in groups)
        {
            if (group.Key == _groupKey)
            {
                self = group;
            }
        }

        // База доли — по виду этой группы: доля расходной группы в доходах бессмысленна
        if (self is not null)
        {
            monthTotal = ReportRowItem.ShareBase(groups.Where(group => group.Kind == self.Kind), currency);
        }

        IsEmpty = self is null || rows.Count is 0;
        IsLoaded = true;

        if (self is null)
        {
            Caption = Month.Caption;
            Total = string.Empty;
            return;
        }

        Money shareBase = ReportRowItem.ShareBase(rows, currency);

        foreach (ReportTotal row in rows)
        {
            Rows.Add(ReportRowItem.From(row, shareBase));
        }

        ReportRowItem share = ReportRowItem.From(self, monthTotal);
        string ofWhat = self.Kind is CategoryKind.Expense ? UiTexts.KindExpenseGenitive : UiTexts.KindIncomeGenitive;

        Name = self.Name;
        Caption = share.HasShare ? $"{Month.Caption} · {share.Share} {ofWhat}" : Month.Caption;
        Total = share.Amount;
        IsTotalExpense = share.IsExpense;
    }

    /// <inheritdoc />
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Categories | DataChange.Accounts;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync(_groupKey, Month);
}
