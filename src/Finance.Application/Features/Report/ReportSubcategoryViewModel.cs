using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Report;

/// <summary>
/// Третий уровень отчёта: операции подкатегории за месяц. Те же операции, что
/// в ленте, — другой разрез, а не копия: строка открывает ту же карточку.
/// </summary>
public sealed partial class ReportSubcategoryViewModel : ScreenViewModel
{
    private readonly IReportQuery _report;
    private readonly ICategoriesQuery _categories;

    private Guid _subcategoryKey;
    private int _generation;

    /// <summary>Создаёт модель представления подкатегории отчёта.</summary>
    /// <param name="report">Суммы и операции отчёта.</param>
    /// <param name="categories">Справочник категорий — за названием подкатегории.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public ReportSubcategoryViewModel(IReportQuery report, ICategoriesQuery categories, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(categories);

        _report = report;
        _categories = categories;
    }

    /// <summary>Операции подкатегории, от новых к старым.</summary>
    public ObservableCollection<ReportTransactionItem> Rows { get; } = [];

    /// <summary>Месяц отчёта — приходит параметром перехода.</summary>
    public ReportMonth Month { get; private set; }

    /// <summary>Название подкатегории — заголовок экрана.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    /// <summary>Подпись под заголовком: «август 2026 · 8 операций».</summary>
    [ObservableProperty]
    public partial string Caption { get; private set; } = string.Empty;

    /// <summary>Сумма подкатегории за месяц со знаком.</summary>
    [ObservableProperty]
    public partial string Total { get; private set; } = string.Empty;

    /// <summary>Операций за месяц нет: они переехали или удалены после перехода сюда.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Идёт чтение.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>
    /// Уровень прочитан хотя бы раз. До этого сказать «операций нет» нельзя:
    /// пустое состояние мигнуло бы и сменилось списком операций.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Есть что показать: список и итог видны.</summary>
    public bool HasItems => IsLoaded && !IsEmpty;

    /// <summary>Читает операции подкатегории за месяц.</summary>
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

        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            IReadOnlyList<ReportTransaction> items = await _report.ReadTransactionsAsync(subcategoryKey, month, cancellationToken);

            // Название — из справочника: он читается целиком и весь помещается
            // в памяти, и заводить ради одного поля четвёртый запрос незачем
            IReadOnlyList<CategoryListItem> categories = await _categories.ReadAsync(cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            Rebuild(items, categories);
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
            }
        }
    }

    private void Rebuild(IReadOnlyList<ReportTransaction> items, IReadOnlyList<CategoryListItem> categories)
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
        Money total = Money.Zero(Currency.RUB);

        foreach (ReportTransaction item in items)
        {
            total += item.Amount;
            Rows.Add(ReportTransactionItem.From(item, name));
        }

        Name = name;
        Total = total.DisplaySigned;
        Caption = $"{Month.Caption} · {Plural.Of(items.Count, "операция", "операции", "операций")}";
        IsEmpty = Rows.Count is 0;
        IsLoaded = true;
    }

    /// <summary>Место стоит заголовком строки, поэтому и его переименование устаревает экран.</summary>
    protected override DataChange Watched =>
        DataChange.Transactions | DataChange.Categories | DataChange.Accounts | DataChange.Places;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync(_subcategoryKey, Month);
}
