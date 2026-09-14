using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Domain;

namespace Finance.Application.Features.Report;

/// <summary>
/// Отчёт за месяц: группы выбранного вида с суммами и долями. Месяц по умолчанию
/// текущий, вид — расходы: так экран отвечает на вопрос «куда ушли деньги»
/// без единого нажатия.
/// </summary>
public sealed partial class ReportViewModel : ScreenViewModel
{
    private static readonly CategoryKind[] KindOrder = [CategoryKind.Expense, CategoryKind.Income];

    private readonly IReportQuery _report;
    private readonly IClock _clock;

    private IReadOnlyList<ReportTotal> _all = [];

    /// <summary>Создаёт модель представления отчёта.</summary>
    /// <param name="report">Суммы отчёта.</param>
    /// <param name="clock">Часы приложения: от них зависит, какой месяц текущий.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ReportViewModel(IReportQuery report, IClock clock, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(clock);

        _report = report;
        _clock = clock;
        Month = ReportMonth.Current(clock);
    }

    /// <summary>Группы выбранного вида, по убыванию суммы.</summary>
    public ObservableCollection<ReportRowItem> Rows { get; } = [];

    /// <summary>Показанный месяц.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle))]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand))]
    public partial ReportMonth Month { get; private set; }

    /// <summary>Какой вид показан.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyTitle))]
    public partial CategoryKind Kind { get; set; } = CategoryKind.Expense;

    /// <summary>Итог месяца со знаком: «−84 260,00 ₽».</summary>
    [ObservableProperty]
    public partial string Total { get; private set; } = string.Empty;

    /// <summary>Операций этого вида за месяц нет — показывается пустое состояние.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Идёт чтение.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Есть что показать: список и итог видны.</summary>
    public bool HasItems => !IsEmpty;

    /// <summary>Шапка переключателя месяцев: «Август 2026».</summary>
    public string MonthTitle => Month.Title;

    /// <summary>Заголовок пустого состояния — свой для каждого вида.</summary>
    public string EmptyTitle => Kind is CategoryKind.Expense
        ? "В этом месяце трат не было"
        : "В этом месяце доходов не было";

    /// <summary>Выбранный вид — номером в переключателе.</summary>
    public int KindIndex
    {
        get => Array.IndexOf(KindOrder, Kind);
        set => Kind = KindOrder[Math.Clamp(value, 0, KindOrder.Length - 1)];
    }

    /// <summary>
    /// Вперёд идти есть куда: показан не текущий месяц. Считается от часов на каждом
    /// обращении, а не запоминается: приложение живёт дольше суток, и запомненный
    /// ответ остался бы прежним после полуночи первого числа.
    /// </summary>
    public bool CanGoForward => Month.First < ReportMonth.Current(_clock).First;

    /// <summary>Перечитывает отчёт за показанный месяц.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            _all = await _report.ReadGroupsAsync(Month, cancellationToken);

            Rebuild();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Предыдущий месяц.</summary>
    [RelayCommand]
    private Task PreviousMonthAsync()
    {
        Month = Month.Previous;

        return LoadAsync();
    }

    /// <summary>Следующий месяц. На текущем команда недоступна — операций в будущем не бывает.</summary>
    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task NextMonthAsync()
    {
        Month = Month.Next;

        return LoadAsync();
    }

    /// <summary>
    /// Пересобирает список под выбранный вид. Из прочитанного, а не из базы:
    /// запрос отдаёт оба вида, а переключатель нажимают подряд.
    /// </summary>
    partial void OnKindChanged(CategoryKind value) => Rebuild();

    private void Rebuild()
    {
        Rows.Clear();

        List<ReportTotal> shown = [];
        Money total = Money.Zero(Currency.RUB);

        foreach (ReportTotal row in _all)
        {
            if (row.Kind == Kind)
            {
                shown.Add(row);
                total += row.Total;
            }
        }

        // Итог — сложение показанных строк, а не второй запрос: строки уровня
        // получены все до одной, страниц у него нет, и повторный проход по той же
        // таблице дал бы ровно это число
        foreach (ReportTotal row in shown)
        {
            Rows.Add(ReportRowItem.From(row, total));
        }

        Total = ReportRowItem.Signed(total, Kind).DisplaySigned;
        IsEmpty = Rows.Count is 0;

        OnPropertyChanged(nameof(KindIndex));
    }

    /// <summary>
    /// Места не в списке: их на первых уровнях не показывают. Счета — обязательно:
    /// снятый признак «скрыть из расчётов» меняет каждое число экрана.
    /// </summary>
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Categories | DataChange.Accounts;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
