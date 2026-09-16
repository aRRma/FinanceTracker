using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Выбор подкатегории для формы операции (экран B-03). Группы свёрнуты: подкатегорий
/// шесть десятков, и сплошной список читать дольше, чем открыть нужную группу.
/// Кто ищет глазами — разворачивает группу, кто помнит название — набирает его
/// в поиске, и тогда группы раскрываются сами.
/// </summary>
public sealed partial class CategoryPickerViewModel : ObservableObject
{
    private readonly ICategoriesQuery _categories;
    private readonly TransactionPicks _picks;

    private readonly List<Branch> _branches = [];
    private readonly HashSet<Guid> _expanded = [];

    private Guid? _selected;

    /// <summary>Создаёт модель представления выбора подкатегории.</summary>
    /// <param name="categories">Справочник категорий обоих уровней.</param>
    /// <param name="picks">Куда кладётся выбор для формы операции.</param>
    public CategoryPickerViewModel(ICategoriesQuery categories, TransactionPicks picks)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(picks);

        _categories = categories;
        _picks = picks;
    }

    /// <summary>Строки списка: шапки групп и подкатегории развёрнутых групп.</summary>
    public ObservableCollection<CategoryPickerLine> Lines { get; } = [];

    /// <summary>Заголовок экрана: категория расхода или дохода.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "Категория";

    /// <summary>Идёт чтение.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Набранное в поиске. Пока поле пусто, группы стоят как их оставили;
    /// с первой же буквой показываются подходящие подкатегории всех групп.
    /// </summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>Справочник прочитан — до этого пустой список ещё ничего не значит.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Ни одна подкатегория не подошла под набранное.</summary>
    public bool IsEmpty => IsLoaded && Lines.Count is 0;

    /// <summary>Читает категории выбранного вида.</summary>
    /// <param name="kind">Вид: расход или доход — он задан видом операции.</param>
    /// <param name="selected">Подкатегория, стоящая в форме сейчас.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CategoryKind kind, Guid? selected, CancellationToken cancellationToken = default)
    {
        Title = kind is CategoryKind.Expense ? "Категория расхода" : "Категория дохода";
        _selected = selected;

        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: следом наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            IReadOnlyList<CategoryListItem> all = await _categories.ReadAsync(cancellationToken);

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

    /// <summary>Запоминает выбор: форма заберёт его, когда вернётся на экран.</summary>
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

    /// <summary>Пересобирает список из прочитанного — в базу за этим не ходят.</summary>
    partial void OnFilterChanged(string value) => Rebuild();

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
