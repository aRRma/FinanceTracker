using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Categories.Catalog;

/// <summary>
/// Справочник категорий: группы выбранного вида, под каждой — её подкатегории.
/// Расходы и доходы показываются порознь: вместе список вдвое длиннее, а ищут
/// в нём всегда что-то одно.
/// </summary>
public sealed partial class CategoriesViewModel : ScreenViewModel
{
    private static readonly CategoryKind[] KindOrder = [CategoryKind.Expense, CategoryKind.Income];

    private readonly ICategoriesQuery _categories;

    private IReadOnlyList<CategoryListItem> _all = [];

    /// <summary>Создаёт модель представления справочника категорий.</summary>
    /// <param name="categories">Список категорий обоих уровней.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public CategoriesViewModel(ICategoriesQuery categories, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(categories);

        _categories = categories;
    }

    /// <summary>Группы выбранного вида со своими подкатегориями.</summary>
    public ObservableCollection<CategoryGroupItem> Groups { get; } = [];

    /// <summary>Подписи видов для переключателя.</summary>
    public static IReadOnlyList<string> KindNames { get; } = ["Расходы", "Доходы"];

    /// <summary>Какой вид показан.</summary>
    [ObservableProperty]
    public partial CategoryKind Kind { get; set; } = CategoryKind.Expense;

    /// <summary>Идёт чтение.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Выбранный вид — номером в переключателе.</summary>
    public int KindIndex
    {
        get => Array.IndexOf(KindOrder, Kind);
        set => Kind = KindOrder[Math.Clamp(value, 0, KindOrder.Length - 1)];
    }

    /// <summary>Групп этого вида нет — показывается пустое состояние.</summary>
    public bool IsEmpty => Groups.Count is 0;

    /// <summary>Перечитывает справочник.</summary>
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
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Пересобирает список под выбранный вид. Из прочитанного, а не из базы:
    /// переключатель видов нажимают подряд, и каждое нажатие стоило бы запроса.
    /// </summary>
    partial void OnKindChanged(CategoryKind value) => Rebuild();

    private void Rebuild()
    {
        Groups.Clear();

        foreach (CategoryListItem group in _all.Where(group => group.IsGroup && group.Kind == Kind))
        {
            IEnumerable<CategoryRowItem> subcategories = _all
                .Where(item => item.ParentKey == group.Key)
                .Select(CategoryRowItem.From);

            Groups.Add(new CategoryGroupItem(group, subcategories));
        }

        OnPropertyChanged(nameof(KindIndex));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Categories;

    /// <inheritdoc />
    protected override void Reload() => LoadCommand.Execute(null);
}
