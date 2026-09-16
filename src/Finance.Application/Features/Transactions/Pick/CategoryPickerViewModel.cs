using Finance.Application.Texts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Выбор подкатегории для формы операции (экран B-03). Группы свёрнуты: подкатегорий
/// шесть десятков, и сплошной список читать дольше, чем открыть нужную группу.
/// Кто ищет глазами — разворачивает группу, кто помнит название — набирает его
/// в поиске, и тогда группы раскрываются сами. Над списком — панель частых:
/// расход записывают несколько раз в день, и почти всегда в одну из немногих
/// подкатегорий, а до них иначе два касания и прокрутка.
/// </summary>
public sealed partial class CategoryPickerViewModel : ObservableObject
{
    /// <summary>
    /// Сколько частых подкатегорий показывает панель.
    /// </summary>
    /// <remarks>
    /// Восемь: столько чипов проматывается пальцем за один жест, а дальше панель
    /// перестаёт быть короткой дорогой и превращается во второй список.
    /// </remarks>
    private const int FrequentCount = 8;

    private readonly ICategoriesQuery _categories;
    private readonly IFrequentCategoriesQuery _frequent;
    private readonly TransactionPicks _picks;

    private readonly List<Branch> _branches = [];
    private readonly HashSet<Guid> _expanded = [];

    private Guid? _selected;

    /// <summary>
    /// Создаёт модель представления выбора подкатегории.
    /// </summary>
    /// <param name="categories">Справочник категорий обоих уровней.</param>
    /// <param name="frequent">Частые подкатегории для панели над списком.</param>
    /// <param name="picks">Куда кладётся выбор для формы операции.</param>
    public CategoryPickerViewModel(
        ICategoriesQuery categories,
        IFrequentCategoriesQuery frequent,
        TransactionPicks picks)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(frequent);
        ArgumentNullException.ThrowIfNull(picks);

        _categories = categories;
        _frequent = frequent;
        _picks = picks;
    }

    /// <summary>
    /// Строки списка: шапки групп и подкатегории развёрнутых групп.
    /// </summary>
    public ObservableCollection<CategoryPickerLine> Lines { get; } = [];

    /// <summary>
    /// Частые подкатегории панели над списком, от частых к редким. Строки те же,
    /// что и в списке: выбор с панели и выбор из списка — одно и то же действие.
    /// </summary>
    public ObservableCollection<CategoryPickerLine> Frequent { get; } = [];

    /// <summary>
    /// Заголовок экрана: категория расхода или дохода.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = UiTexts.PickCategoryTitle;

    /// <summary>
    /// Идёт чтение.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Набранное в поиске. Пока поле пусто, группы стоят как их оставили;
    /// с первой же буквой показываются подходящие подкатегории всех групп.
    /// </summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Справочник прочитан — до этого пустой список ещё ничего не значит.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Ни одна подкатегория не подошла под набранное.
    /// </summary>
    public bool IsEmpty => IsLoaded && Lines.Count is 0;

    /// <summary>
    /// Панель частых видна: показывать есть что и в поиске ничего не набрано.
    /// </summary>
    /// <remarks>
    /// С первой же буквой панель уходит: набравший буквы ищет как раз не частое,
    /// а список под панелью поднимается к первой подходящей строке.
    /// </remarks>
    public bool IsFrequentVisible => Frequent.Count > 0 && Filter.AsSpan().Trim().Length is 0;

    /// <summary>
    /// Читает категории выбранного вида.
    /// </summary>
    /// <param name="kind">Вид: расход или доход — он задан видом операции.</param>
    /// <param name="selected">Подкатегория, стоящая в форме сейчас.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CategoryKind kind, Guid? selected, CancellationToken cancellationToken = default)
    {
        Title = kind is CategoryKind.Expense ? UiTexts.PickCategoryExpense : UiTexts.PickCategoryIncome;
        _selected = selected;

        IsBusy = true;

        try
        {
            // Два чтения независимы и идут разом: у каждого запроса свой контекст,
            // а последовательно экран ждал бы сумму двух обращений к базе
            Task<IReadOnlyList<CategoryListItem>> categoriesTask = _categories.ReadAsync(cancellationToken);
            Task<IReadOnlyList<FrequentCategory>> frequentTask =
                _frequent.ReadAsync(kind, FrequentCount, cancellationToken);

            // ConfigureAwait(false) здесь недопустим: следом наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            await Task.WhenAll(categoriesTask, frequentTask);

            IReadOnlyList<CategoryListItem> all = await categoriesTask;
            IReadOnlyList<FrequentCategory> frequent = await frequentTask;

            Frequent.Clear();

            foreach (FrequentCategory item in frequent)
            {
                // Приглушения в панели нет: сюда попадает только то, чем и правда
                // пользуются, и «Прочее» здесь такая же рабочая подкатегория
                Frequent.Add(new CategoryPickerLine(
                    item.Key,
                    item.Icon,
                    item.Name,
                    isGroup: false,
                    count: 0,
                    isProtected: false,
                    item.Key == selected));
            }

            _branches.Clear();
            _expanded.Clear();

            // Дети раскладываются по группам одним проходом, а не проходом по всему справочнику на каждую группу
            ILookup<Guid?, CategoryListItem> byParent = all.ToLookup(static item => item.ParentKey);

            foreach (CategoryListItem group in all.Where(item => item.IsGroup && item.Kind == kind))
            {
                CategoryListItem[] children = [.. byParent[group.Key]];

                _branches.Add(new Branch(group, children));

                // Группа выбранной подкатегории открыта сразу: иначе непонятно,
                // где стоит текущий выбор, и его приходится искать вслепую
                if (selected is { } key && Array.Exists(children, child => child.Key == key))
                {
                    _expanded.Add(group.Key);
                }
            }

            Rebuild();

            IsLoaded = true;

            OnPropertyChanged(nameof(IsFrequentVisible));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Разворачивает или сворачивает группу. Во время поиска не действует: там
    /// показано только подходящее, и сворачивать нечего.
    /// </summary>
    /// <remarks>
    /// Строки вставляются и удаляются поштучно, а не пересобирается весь список:
    /// полная пересборка приходит в список как сброс, и тот перерисовывает себя
    /// целиком, без плавного появления строк.
    /// </remarks>
    /// <param name="line">Строка-шапка группы.</param>
    [RelayCommand]
    public void Toggle(CategoryPickerLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (!line.IsGroup || Filter.AsSpan().Trim().Length > 0)
        {
            return;
        }

        int index = Lines.IndexOf(line);

        // Строки под руками нет — список успели перечитать; следующее
        // касание придёт уже по новой строке
        if (index < 0)
        {
            return;
        }

        if (_expanded.Add(line.Key))
        {
            int next = index + 1;

            foreach (CategoryListItem child in Children(line.Key))
            {
                Lines.Insert(next, Subcategory(child));
                next++;
            }
        }
        else
        {
            _expanded.Remove(line.Key);

            // Подкатегории лежат подряд сразу под шапкой: конец группы —
            // следующая шапка или конец списка
            while (index + 1 < Lines.Count && Lines[index + 1].IsSubcategory)
            {
                Lines.RemoveAt(index + 1);
            }
        }

        line.IsExpanded = _expanded.Contains(line.Key);

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Запоминает выбор: форма заберёт его, когда вернётся на экран.
    /// </summary>
    /// <param name="line">Выбранная подкатегория.</param>
    [RelayCommand]
    public void Pick(CategoryPickerLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.IsSubcategory)
        {
            _picks.Category = line.Key;
        }
    }

    /// <summary>
    /// Пересобирает список из прочитанного — в базу за этим не ходят.
    /// </summary>
    partial void OnFilterChanged(string value)
    {
        Rebuild();

        OnPropertyChanged(nameof(IsFrequentVisible));
    }

    private void Rebuild()
    {
        Lines.Clear();

        string filter = Filter.Trim();
        bool searching = filter.Length > 0;

        foreach (Branch branch in _branches)
        {
            // Совпало название группы — показывается вся группа: искали её
            bool groupMatches = searching && Matches(branch.Group.Name, filter);

            CategoryListItem[] children = searching && !groupMatches
                ? [.. branch.Children.Where(child => Matches(child.Name, filter))]
                : branch.Children;

            if (searching && children.Length is 0)
            {
                continue;
            }

            bool expanded = searching || _expanded.Contains(branch.Group.Key);

            Lines.Add(new CategoryPickerLine(
                branch.Group.Key,
                branch.Group.Icon,
                branch.Group.Name,
                isGroup: true,
                branch.Children.Length,
                branch.Group.Role is CategoryRole.Service,
                isSelected: false)
            {
                IsExpanded = expanded
            });

            if (!expanded)
            {
                continue;
            }

            foreach (CategoryListItem child in children)
            {
                Lines.Add(Subcategory(child));
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    // Подкатегории группы в том же порядке, в каком их кладёт пересборка:
    // разворот обязан дать тот же список, что и чтение
    private CategoryListItem[] Children(Guid group)
    {
        foreach (Branch branch in _branches)
        {
            if (branch.Group.Key == group)
            {
                return branch.Children;
            }
        }

        return [];
    }

    private CategoryPickerLine Subcategory(CategoryListItem child) =>
        new(child.Key, child.Icon, child.Name, isGroup: false, count: 0, child.IsProtected, child.Key == _selected);

    // Сравнение по культуре, а не по порядку: «ё» и регистр иначе разошлись бы
    private static bool Matches(string name, string filter) =>
        name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    private readonly record struct Branch(CategoryListItem Group, CategoryListItem[] Children);
}
