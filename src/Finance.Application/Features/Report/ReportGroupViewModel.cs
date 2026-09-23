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

    private Guid _groupKey;
    private int _generation;

    /// <summary>
    /// Создаёт модель представления группы отчёта.
    /// </summary>
    /// <param name="report">Суммы отчёта.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ReportGroupViewModel(IReportQuery report, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(report);

        _report = report;
    }

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
    /// Идёт чтение.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

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

        IsBusy = true;

        try
        {
            // Оба чтения независимы и идут разом: у каждого свой контекст,
            // а последовательно экран ждал бы сумму двух обращений к базе
            Task<IReadOnlyList<ReportTotal>> groupsTask = _report.ReadGroupsAsync(month, cancellationToken);
            Task<IReadOnlyList<ReportTotal>> rowsTask = _report.ReadSubcategoriesAsync(groupKey, month, cancellationToken);

            // ConfigureAwait(false) здесь недопустим: дальше наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            await Task.WhenAll(groupsTask, rowsTask);

            IReadOnlyList<ReportTotal> groups = await groupsTask;
            IReadOnlyList<ReportTotal> rows = await rowsTask;

            if (generation != _generation)
            {
                return;
            }

            Rebuild(groups, rows);
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
            }
        }
    }

    private void Rebuild(IReadOnlyList<ReportTotal> groups, IReadOnlyList<ReportTotal> rows)
    {
        Rows.Clear();

        ReportTotal? self = null;
        Money monthTotal = Money.Zero(Currency.RUB);

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
            monthTotal = ReportRowItem.ShareBase(groups.Where(group => group.Kind == self.Kind));
        }

        IsEmpty = self is null || rows.Count is 0;
        IsLoaded = true;

        if (self is null)
        {
            Caption = Month.Caption;
            Total = string.Empty;
            return;
        }

        Money shareBase = ReportRowItem.ShareBase(rows);

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
