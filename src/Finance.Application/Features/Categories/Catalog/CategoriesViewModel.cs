using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Categories.Catalog;

/// <summary>
/// Справочник категорий: группы выбранного вида, под каждой — её подкатегории.
/// Расходы и доходы показываются порознь: вместе список вдвое длиннее, а ищут
/// в нём всегда что-то одно. Группы свёрнуты: тринадцать групп с шестью десятками
/// подкатегорий листать дольше, чем открыть нужную.
/// </summary>
public sealed partial class CategoriesViewModel : ScreenViewModel
{
    private static readonly CategoryKind[] KindOrder = [CategoryKind.Expense, CategoryKind.Income];

    private readonly ICategoriesQuery _categories;
    private readonly HashSet<Guid> _expanded = [];

    private IReadOnlyList<CategoryListItem> _all = [];

    /// <summary>
    /// Создаёт модель представления справочника категорий.
    /// </summary>
    /// <param name="categories">Список категорий обоих уровней.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public CategoriesViewModel(ICategoriesQuery categories, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(categories);

        _categories = categories;
    }

    /// <summary>
    /// Строки списка: шапки групп и подкатегории развёрнутых групп.
    /// </summary>
    public ObservableCollection<CategoryLine> Lines { get; } = [];

    /// <summary>
    /// Подписи видов для переключателя.
    /// </summary>
    public static IReadOnlyList<string> KindNames { get; } = ["Расходы", "Доходы"];

    /// <summary>
    /// Какой вид показан.
    /// </summary>
    [ObservableProperty]
    public partial CategoryKind Kind { get; set; } = CategoryKind.Expense;

    /// <summary>
    /// Набранное в поиске. Пока поле пусто, группы стоят как их оставили;
    /// с первой же буквой показываются подходящие подкатегории всех групп.
    /// </summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Идёт чтение. Запись открыта намеренно: к этому признаку привязан жест
    /// «потянуть вниз», и он сам поднимает его в начале обновления.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Выбранный вид — номером в переключателе.
    /// </summary>
    public int KindIndex
    {
        get => Array.IndexOf(KindOrder, Kind);
        set => Kind = KindOrder[Math.Clamp(value, 0, KindOrder.Length - 1)];
    }

    /// <summary>
    /// Справочник прочитан хотя бы раз. Пустая коллекция до чтения значит «ещё
    /// не читали», а не «групп нет», и приглашение завести первую мигнуло бы
    /// на каждом заходе.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Групп этого вида нет — показывается пустое состояние.
    /// </summary>
    public bool IsEmpty => IsLoaded && Lines.Count is 0;

    /// <summary>
    /// Перечитывает справочник.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: дальше наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            _all = await _categories.ReadAsync(cancellationToken);

            Rebuild();

            // Не в Rebuild: его же зовёт переключатель вида, а он о чтении ничего не говорит
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
    public void Toggle(CategoryLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (!line.IsGroup || Filter.AsSpan().Trim().Length > 0)
        {
            return;
        }

        int index = Lines.IndexOf(line);

        // Строки под руками нет — экран успели перечитать; следующее
        // касание придёт уже по новой строке
        if (index < 0)
        {
            return;
        }

        if (_expanded.Add(line.Key))
        {
            int next = index + 1;

            foreach (CategoryListItem child in _all.Where(item => item.ParentKey == line.Key))
            {
                Lines.Insert(next, CategoryLine.Subcategory(child));
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
    /// Пересобирает список под выбранный вид. Из прочитанного, а не из базы:
    /// переключатель видов нажимают подряд, и каждое нажатие стоило бы запроса.
    /// </summary>
    partial void OnKindChanged(CategoryKind value) => Rebuild();

    /// <summary>
    /// Отбор идёт по прочитанному — в базу за ним не ходят.
    /// </summary>
    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        Lines.Clear();

        string filter = Filter.Trim();
        bool searching = filter.Length > 0;

        // Дети раскладываются по группам одним проходом: пересборку дёргают на каждую
        // букву, и проход по всему справочнику на каждую группу здесь лишний
        ILookup<Guid?, CategoryListItem> byParent = _all.ToLookup(static item => item.ParentKey);

        foreach (CategoryListItem group in _all.Where(item => item.IsGroup && item.Kind == Kind))
        {
            CategoryListItem[] children = [.. byParent[group.Key]];

            // Совпало название группы — показывается вся группа: искали её
            bool groupMatches = searching && Matches(group.Name, filter);

            CategoryListItem[] shown = searching && !groupMatches
                ? [.. children.Where(child => Matches(child.Name, filter))]
                : children;

            if (searching && shown.Length is 0)
            {
                continue;
            }

            bool expanded = searching || _expanded.Contains(group.Key);

            CategoryLine header = CategoryLine.Group(group, children.Length);
            header.IsExpanded = expanded;

            Lines.Add(header);

            if (!expanded)
            {
                continue;
            }

            foreach (CategoryListItem child in shown)
            {
                Lines.Add(CategoryLine.Subcategory(child));
            }
        }

        OnPropertyChanged(nameof(KindIndex));
        OnPropertyChanged(nameof(IsEmpty));
    }

    // Сравнение по культуре, а не по порядку: «ё» и регистр иначе разошлись бы
    private static bool Matches(string name, string filter) =>
        name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Categories;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
