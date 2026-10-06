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
    private readonly IReportQuery _report;
    private readonly IReportAccountsQuery _accounts;
    private readonly ReportChoice _choice;
    private readonly IClock _clock;

    private IReadOnlyList<ReportTotal> _all = [];

    // Валюта прочитанного: переключатель вида пересобирает строки без чтения,
    // и итог обязан сложиться в той же валюте, в какой их прочитали
    private Currency _currency = Currency.RUB;

    // Номер чтения: по нему завершившееся чтение узнаёт, что месяц за время
    // запроса сменили ещё раз и его строки уже не к месту. Без него два быстрых
    // нажатия «назад» показали бы группы одного месяца под шапкой другого
    private int _generation;

    /// <summary>
    /// Создаёт модель представления отчёта.
    /// </summary>
    /// <param name="report">Суммы отчёта.</param>
    /// <param name="accounts">Счета — подпись и знаки строки счетов.</param>
    /// <param name="choice">Выбор счетов отчёта.</param>
    /// <param name="clock">Часы приложения: от них зависит, какой месяц текущий.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ReportViewModel(
        IReportQuery report,
        IReportAccountsQuery accounts,
        ReportChoice choice,
        IClock clock,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(clock);

        _report = report;
        _accounts = accounts;
        _choice = choice;
        _clock = clock;
        Month = ReportMonth.Current(clock);
    }

    /// <summary>
    /// Строка счетов под месяцем: по каким счетам посчитан отчёт.
    /// </summary>
    [ObservableProperty]
    public partial ReportAccountsLine Accounts { get; private set; } = ReportAccountsLine.Empty;

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
    /// Отчёт прочитан хотя бы раз. До этого сказать «операций не было» нельзя:
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
    /// Подсказка под заголовком пустого состояния — сменить месяц. Пуста, когда
    /// операции месяца есть, но по счетам вне отчёта: другой месяц тут не поможет.
    /// Заголовок же один — «Нет операций по этим счетам»: строка счетов стоит прямо
    /// над ним, и фраза верна при любом выборе.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyHint))]
    public partial string EmptyHint { get; private set; } = UiTexts.ReportEmptyOtherMonth;

    /// <summary>
    /// Показывать ли подсказку под заголовком пустого состояния.
    /// </summary>
    public bool HasEmptyHint => EmptyHint.Length > 0;

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
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        int generation = ++_generation;

        // Выбор берётся один раз на чтение: все запросы этого чтения обязаны
        // считать по одному набору, даже если выбор сменят посреди него
        ReportAccounts accounts = _choice.Accounts;

        // Суммы и счета строки независимы и идут разом: у каждого свой контекст.
        // ConfigureAwait(false) здесь недопустим: дальше наполняются
        // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
        Task<IReadOnlyList<ReportTotal>> groupsTask = _report.ReadGroupsAsync(Month, accounts, cancellationToken);
        Task<IReadOnlyList<ReportAccount>> listTask = _accounts.ReadAsync(cancellationToken);

        await Task.WhenAll(groupsTask, listTask);

        IReadOnlyList<ReportTotal> all = await groupsTask;
        IReadOnlyList<ReportAccount> list = await listTask;

        // Ни одного счёта набора не осталось — удалены, сменили валюту или, у «всех
        // активных» другой валюты, ушли в накопления: такой набор ни о чём, и отчёт
        // возвращается к умолчанию, а не показывает пустоту с подписью «0 счетов»
        // или «Активные счета · €» без единого счёта
        if (!accounts.IsDefault && !list.Any(accounts.Includes))
        {
            if (generation == _generation && ReferenceEquals(_choice.Accounts, accounts))
            {
                _choice.Accounts = ReportAccounts.Default;
                await LoadAsync(cancellationToken);
            }

            return;
        }

        // Третий запрос только когда отчёт пуст целиком: при строках пустое состояние не видно
        bool hasUncounted = all.Count is 0 && await _report.HasUncountedAsync(Month, accounts, cancellationToken);

        if (generation != _generation)
        {
            return;
        }

        _all = all;
        _currency = accounts.Currency;
        Accounts = ReportAccountsLine.Of(accounts, list);
        EmptyHint = hasUncounted ? string.Empty : UiTexts.ReportEmptyOtherMonth;

        Rebuild();

        // Не в Rebuild: его же зовёт переключатель вида, а он о чтении ничего не говорит
        IsLoaded = true;
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
        Money total = Money.Zero(_currency);

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
        Money shareBase = ReportRowItem.ShareBase(shown, _currency);

        foreach (ReportTotal row in shown)
        {
            Rows.Add(ReportRowItem.From(row, shareBase));
        }

        Money signed = ReportRowItem.Signed(total, Kind);

        Total = signed.DisplaySigned;
        IsTotalExpense = signed.Amount < 0m;
        IsEmpty = Rows.Count is 0;
    }

    /// <summary>
    /// Места не в списке: их на первых уровнях не показывают. Счета — обязательно:
    /// снятый признак «скрытый» меняет каждое число экрана.
    /// </summary>
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Categories | DataChange.Accounts;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
