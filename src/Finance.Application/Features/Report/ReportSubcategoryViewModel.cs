using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Features.Report;

/// <summary>
/// Третий уровень отчёта: операции подкатегории за месяц. Те же операции, что
/// в ленте, — другой разрез, а не копия: строка открывает ту же карточку.
/// </summary>
public sealed partial class ReportSubcategoryViewModel : ScreenViewModel
{
    private readonly IReportQuery _report;
    private readonly ICategoriesQuery _categories;
    private readonly IReportAccountsQuery _accounts;
    private readonly ReportChoice _choice;

    private Guid _subcategoryKey;
    private int _generation;

    /// <summary>
    /// Создаёт модель представления подкатегории отчёта.
    /// </summary>
    /// <param name="report">Суммы и операции отчёта.</param>
    /// <param name="categories">Справочник категорий — за названием подкатегории.</param>
    /// <param name="accounts">Счета — знаки набора в подписи.</param>
    /// <param name="choice">Выбор счетов отчёта.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ReportSubcategoryViewModel(
        IReportQuery report,
        ICategoriesQuery categories,
        IReportAccountsQuery accounts,
        ReportChoice choice,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(choice);

        _report = report;
        _categories = categories;
        _accounts = accounts;
        _choice = choice;
    }

    /// <summary>
    /// Знаки счетов набора справа в подписи — только при своём наборе.
    /// </summary>
    [ObservableProperty]
    public partial ReportAccountsLine Accounts { get; private set; } = ReportAccountsLine.Empty;

    /// <summary>
    /// Операции подкатегории, от новых к старым.
    /// </summary>
    public ObservableCollection<ReportTransactionItem> Rows { get; } = [];

    /// <summary>
    /// Месяц отчёта — приходит параметром перехода.
    /// </summary>
    public ReportMonth Month { get; private set; }

    /// <summary>
    /// Название подкатегории — заголовок экрана.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Подпись под заголовком: «август 2026 · 8 операций».
    /// </summary>
    [ObservableProperty]
    public partial string Caption { get; private set; } = string.Empty;

    /// <summary>
    /// Сумма подкатегории за месяц со знаком.
    /// </summary>
    [ObservableProperty]
    public partial string Total { get; private set; } = string.Empty;

    /// <summary>
    /// Итог — расходный: красится тем же цветом, что строки под ним.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTotalExpense { get; private set; }

    /// <summary>
    /// Операций за месяц нет: они переехали или удалены после перехода сюда.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Уровень прочитан хотя бы раз. До этого сказать «операций нет» нельзя:
    /// пустое состояние мигнуло бы и сменилось списком операций.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Есть что показать: список и итог видны.
    /// </summary>
    public bool HasItems => IsLoaded && !IsEmpty;

    /// <summary>
    /// Читает операции подкатегории за месяц.
    /// </summary>
    /// <param name="subcategoryKey">Ключ подкатегории.</param>
    /// <param name="month">Месяц отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(Guid subcategoryKey, ReportMonth month, CancellationToken cancellationToken = default)
    {
        _subcategoryKey = subcategoryKey;
        Month = month;

        // Номер чтения: перечитывание по чужой правке может обогнать первое чтение,
        // и строки более раннего запроса легли бы поверх более свежих
        int generation = ++_generation;
        ReportAccounts accounts = _choice.Accounts;

        // Название — из справочника: он читается целиком и весь помещается
        // в памяти, и заводить ради одного поля отдельный запрос незачем.
        // Чтения независимы и идут разом: у каждого свой контекст.
        // Счета нужны только знакам своего набора — при обычном их не читают
        Task<IReadOnlyList<ReportTransaction>> itemsTask = _report.ReadTransactionsAsync(subcategoryKey, month, accounts, cancellationToken);
        Task<IReadOnlyList<CategoryListItem>> categoriesTask = _categories.ReadAsync(cancellationToken);
        Task<IReadOnlyList<ReportAccount>> listTask = ReportAccountsLine.ReadForTokensAsync(_accounts, accounts, cancellationToken);

        // ConfigureAwait(false) здесь недопустим: дальше наполняются
        // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
        await Task.WhenAll(itemsTask, categoriesTask, listTask);

        IReadOnlyList<ReportTransaction> items = await itemsTask;
        IReadOnlyList<CategoryListItem> categories = await categoriesTask;
        IReadOnlyList<ReportAccount> list = await listTask;

        if (generation != _generation)
        {
            return;
        }

        Accounts = ReportAccountsLine.Of(accounts, list);
        Rebuild(items, categories, accounts.Currency);
    }

    private void Rebuild(IReadOnlyList<ReportTransaction> items, IReadOnlyList<CategoryListItem> categories, Currency currency)
    {
        Rows.Clear();

        string name = string.Empty;

        foreach (CategoryListItem category in categories)
        {
            if (category.Key == _subcategoryKey)
            {
                name = category.Name;
            }
        }

        // Итог складывается из показанных строк, и это верно ровно потому, что
        // страниц у третьего уровня нет: получены все операции месяца.
        // Появится дочитывание — итог обязан уехать в базу
        Money total = Money.Zero(currency);

        foreach (ReportTransaction item in items)
        {
            total += item.Amount;
            Rows.Add(ReportTransactionItem.From(item, name));
        }

        Name = name;
        Total = total.DisplaySigned;
        IsTotalExpense = !total.IsPositive;
        Caption = $"{Month.Caption} · {Plural.Of(items.Count, UiTexts.ReportTransactionsCountOne, UiTexts.ReportTransactionsCountFew, UiTexts.ReportTransactionsCountMany)}";
        IsEmpty = Rows.Count is 0;
        IsLoaded = true;
    }

    /// <summary>
    /// Место стоит заголовком строки, поэтому и его переименование устаревает экран.
    /// </summary>
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Categories | DataChange.Accounts | DataChange.Places;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync(_subcategoryKey, Month);
}
