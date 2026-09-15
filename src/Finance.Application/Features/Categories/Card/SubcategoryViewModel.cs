using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Карточка подкатегории: заведение, правка, перенос в другую группу и удаление.
/// Вид не показывается для выбора — он наследуется от группы, и переезд в группу
/// другого вида запрещён.
/// </summary>
public sealed partial class SubcategoryViewModel : ObservableObject
{
    private readonly ICategoriesQuery _categories;
    private readonly ISaveCategoryHandler _handler;
    private readonly ICategoryDeletionQuery _deletion;
    private readonly IDeleteSubcategoryHandler _delete;

    private CategoryKind _kind = CategoryKind.Expense;
    private bool _isProtected;

    /// <summary>Создаёт модель представления карточки подкатегории.</summary>
    /// <param name="categories">Список категорий: из него берутся группы для переноса.</param>
    /// <param name="handler">Сохранение категории.</param>
    /// <param name="deletion">Последствия удаления для диалога.</param>
    /// <param name="delete">Удаление подкатегории с переездом операций.</param>
    /// <param name="icons">Набор значков.</param>
    public SubcategoryViewModel(
        ICategoriesQuery categories,
        ISaveCategoryHandler handler,
        ICategoryDeletionQuery deletion,
        IDeleteSubcategoryHandler delete,
        IconCatalog icons)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(deletion);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(icons);

        _categories = categories;
        _handler = handler;
        _deletion = deletion;
        _delete = delete;

        Icon = new IconPicker(icons);
    }

    /// <summary>Ключ правимой подкатегории. Пусто — заводится новая.</summary>
    public Guid? Key { get; private set; }

    /// <summary>Выбор значка подкатегории.</summary>
    public IconPicker Icon { get; }

    /// <summary>Группы, в которые подкатегорию можно перенести: того же вида и не служебные.</summary>
    public ObservableCollection<CategoryGroupOption> Groups { get; } = [];

    /// <summary>Название подкатегории.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Выбранная группа. Смена — это перенос.</summary>
    [ObservableProperty]
    public partial CategoryGroupOption? Group { get; set; }

    /// <summary>Текст нарушенного правила. Пусто — сохранять можно.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Идёт сохранение или удаление. Кнопки зовут методы напрямую, минуя команду
    /// с её защитой от повторного запуска: без флага второе нажатие до ухода
    /// экрана завело бы вторую подкатегорию.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>Сохранять и удалять можно: предыдущее действие не идёт.</summary>
    public bool CanSave => !IsSaving;

    /// <summary>Выбранная группа — номером в списке.</summary>
    public int GroupIndex
    {
        get => Group is null ? -1 : Groups.IndexOf(Group);
        set => Group = value >= 0 && value < Groups.Count ? Groups[value] : null;
    }

    /// <summary>Подпись вида: он наследуется от группы и на этом экране заперт.</summary>
    public string KindCaption => _kind is CategoryKind.Expense ? "Расход" : "Доход";

    /// <summary>Заголовок экрана.</summary>
    public string Title => Key is null ? "Новая подкатегория" : "Подкатегория";

    /// <summary>
    /// Удалять есть что и есть чем: подкатегория существует и не защищена.
    /// У «Прочего» и служебной кнопки удаления не бывает вовсе.
    /// </summary>
    public bool CanDelete => Key is not null && !_isProtected;

    /// <summary>Правило нарушено — сообщение показывается рядом с формой.</summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Загружает подкатегорию для правки или готовит новую в указанной группе.
    /// У новой значок подставляется от группы: свой значок подкатегории —
    /// уточнение, а не обязанность.
    /// </summary>
    /// <param name="key">Ключ подкатегории или <c>null</c> для новой.</param>
    /// <param name="group">Группа новой подкатегории. У существующей берётся её собственная.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <remarks>
    /// Без <c>[RelayCommand]</c>: двух параметров генератор команд не принимает,
    /// а страница зовёт загрузку сама — привязывать её не к чему.
    /// </remarks>
    public async Task LoadAsync(Guid? key, Guid? group, CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
        // коллекция, а её правка вне потока интерфейса роняет разметку
        IReadOnlyList<CategoryListItem> categories = await _categories.ReadAsync(cancellationToken);

        CategoryListItem? subcategory = key is { } existing
            ? categories.FirstOrDefault(item => item.Key == existing && !item.IsGroup)
            : null;

        Guid? parentKey = subcategory?.ParentKey ?? group;

        if (categories.FirstOrDefault(item => item.Key == parentKey && item.IsGroup) is not { } parent)
        {
            return;
        }

        _kind = parent.Kind;

        FillGroups(categories, parent);

        if (subcategory is { } found)
        {
            Key = found.Key;
            Name = found.Name;
            _isProtected = found.IsProtected;
            Icon.Show(found.Icon);
        }
        else
        {
            Icon.Show(parent.Icon);
        }

        Group = Groups.FirstOrDefault(option => option.Key == parent.Key);

        Refresh();
    }

    /// <summary>
    /// Сохраняет подкатегорию. Смена группы в списке — это перенос: обработчик
    /// сверит вид обеих групп и свободу имени в новой.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если подкатегория сохранена и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
        {
            return false;
        }

        Error = null;
        OnPropertyChanged(nameof(HasError));

        if (Group is not { } target)
        {
            Error = "Группа не выбрана";
            OnPropertyChanged(nameof(HasError));

            return false;
        }

        IsSaving = true;
        bool done = false;

        try
        {
            Key = await _handler.HandleAsync(
                new SaveCategoryCommand
                {
                    Key = Key,
                    ParentKey = target.Key,
                    Name = Name,
                    Icon = Icon.Selected
                },
                cancellationToken);

            Refresh();

            done = true;

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;
            OnPropertyChanged(nameof(HasError));

            return false;
        }
        finally
        {
            // После удачи флаг остаётся: экран закрывается, и второе нажатие
            // в этот промежуток записало бы то же самое ещё раз
            IsSaving = !done;
        }
    }

    /// <summary>
    /// Текст подтверждения удаления. Называет число операций и приёмник: переезд
    /// необратим, а по числу видно, та ли это подкатегория.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task<string> DeletePromptAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key || await _deletion.ReadAsync(key, cancellationToken) is not { } deletion)
        {
            return "Отменить удаление будет нельзя.";
        }

        if (deletion.TransactionCount is 0)
        {
            return "Операций в ней нет. Отменить удаление будет нельзя.";
        }

        string operations = Plural.Of(
            deletion.TransactionCount,
            "операция перейдёт",
            "операции перейдут",
            "операций перейдут");

        return $"{operations} в «{deletion.GroupName} · {deletion.ReceiverName}». "
               + "Суммы и даты не изменятся, балансы останутся прежними.";
    }

    /// <summary>Удаляет подкатегорию вместе с переездом её операций.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если подкатегория удалена и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key || IsSaving)
        {
            return false;
        }

        IsSaving = true;
        bool done = false;
        Error = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            await _delete.HandleAsync(key, cancellationToken);

            done = true;

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;
            OnPropertyChanged(nameof(HasError));

            return false;
        }
        finally
        {
            // После удачи флаг остаётся: экран закрывается, и второе нажатие
            // в этот промежуток записало бы то же самое ещё раз
            IsSaving = !done;
        }
    }

    /// <summary>Переносить можно только в группы того же вида и не в служебные.</summary>
    private void FillGroups(IReadOnlyList<CategoryListItem> categories, CategoryListItem parent)
    {
        Groups.Clear();

        foreach (CategoryListItem group in categories.Where(item =>
                     item.IsGroup
                     && item.Kind == parent.Kind
                     && (item.Role is not CategoryRole.Service || item.Key == parent.Key)))
        {
            Groups.Add(new CategoryGroupOption(group.Key, group.Name));
        }
    }

    partial void OnGroupChanged(CategoryGroupOption? value) => OnPropertyChanged(nameof(GroupIndex));

    private void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(KindCaption));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(GroupIndex));
    }
}
