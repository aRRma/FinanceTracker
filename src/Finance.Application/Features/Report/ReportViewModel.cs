using Finance.Application.Texts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Finance.Domain.Values;

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

    // Номер чтения: по нему завершившееся чтение узнаёт, что месяц за время
    // запроса сменили ещё раз и его строки уже не к месту. Без него два быстрых
    // нажатия «назад» показали бы группы одного месяца под шапкой другого
    private int _generation;

    /// <summary>
    /// Создаёт модель представления отчёта.
    /// </summary>
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

    /// <summary>
    /// Группы выбранного вида, по убыванию суммы.
    /// </summary>
    public ObservableCollection<ReportRowItem> Rows { get; } = [];

    /// <summary>
    /// Показанный месяц.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle))]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand))]
    public partial ReportMonth Month { get; private set; }

    /// <summary>
    /// Какой вид показан.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyTitle))]
    public partial CategoryKind Kind { get; set; } = CategoryKind.Expense;

    /// <summary>
    /// Итог месяца со знаком: «−84 260,00 ₽».
    /// </summary>
    [ObservableProperty]
    public partial string Total { get; private set; } = string.Empty;

    /// <summary>
    /// Итог со знаком отрицателен — красится цветом расхода. Идёт за знаком, а не
    /// за видом: месяц одних возвратов сводит расходы в плюс.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTotalExpense { get; private set; }

    /// <summary>
    /// Операций этого вида за месяц нет — показывается пустое состояние.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Идёт чтение. Запись открыта намеренно: к этому признаку привязан жест
    /// «потянуть вниз», и он сам поднимает его в начале обновления.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Отчёт прочитан хотя бы раз. До этого сказать «трат не было» нельзя:
    /// пустое состояние мигнуло бы и сменилось списком групп.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Есть что показать: список и итог видны.
    /// </summary>
    public bool HasItems => IsLoaded && !IsEmpty;

    /// <summary>
    /// Шапка переключателя месяцев: «Август 2026».
    /// </summary>
    public string MonthTitle => Month.Title;

    /// <summary>
    /// Заголовок пустого состояния — свой для каждого вида.
    /// </summary>
    public string EmptyTitle => Kind is CategoryKind.Expense
        ? UiTexts.ReportEmptyExpense
        : UiTexts.ReportEmptyIncome;

    /// <summary>
    /// Подпись пустого состояния. Когда операции месяца есть, но в суммы не вошли,
    /// пустота объясняется правилом отчёта: иначе пользователь с валютным счётом
    /// принял бы пустой отчёт за поломку.
    /// </summary>
    [ObservableProperty]
    public partial string EmptyHint { get; private set; } = UiTexts.ReportEmptyOtherMonth;

    /// <summary>
    /// Выбранный вид — номером в переключателе.
    /// </summary>
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

    /// <summary>
    /// Перечитывает отчёт за показанный месяц.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        int generation = ++_generation;

        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            IReadOnlyList<ReportTotal> all = await _report.ReadGroupsAsync(Month, cancellationToken);

            // Второй запрос только когда отчёт пуст целиком: при строках подсказка не видна
            bool hasUncounted = all.Count is 0 && await _report.HasUncountedAsync(Month, cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            _all = all;
            EmptyHint = hasUncounted ? UiTexts.ReportEmptyCurrency : UiTexts.ReportEmptyOtherMonth;

            Rebuild();

            // Не в Rebuild: его же зовёт переключатель вида, а он о чтении ничего не говорит
            IsLoaded = true;
        }
        finally
        {
            // Занятость снимает только последнее чтение: более раннее ещё не значит,
            // что экран готов
            if (generation == _generation)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Предыдущий месяц.
    /// </summary>
    [RelayCommand]
    private Task PreviousMonthAsync()
    {
        Month = Month.Previous;

        return LoadAsync();
    }

    /// <summary>
    /// Следующий месяц. На текущем команда недоступна — операций в будущем не бывает.
    /// </summary>
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
        Money shareBase = ReportRowItem.ShareBase(shown);

        foreach (ReportTotal row in shown)
        {
            Rows.Add(ReportRowItem.From(row, shareBase));
        }

        Money signed = ReportRowItem.Signed(total, Kind);

        Total = signed.DisplaySigned;
        IsTotalExpense = signed.Amount < 0m;
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
