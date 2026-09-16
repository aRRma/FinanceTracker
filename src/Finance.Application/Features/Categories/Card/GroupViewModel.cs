using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;
using Finance.Domain.Errors;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Карточка группы: заведение и правка одним экраном. Вид выбирается только при
/// заведении и дальше заперт — его наследуют все подкатегории, и смена вида
/// перевернула бы знак у всей их истории.
/// </summary>
public sealed partial class GroupViewModel : ObservableObject, IFormModel
{
    private static readonly CategoryKind[] KindOrder = [CategoryKind.Expense, CategoryKind.Income];

    private readonly ICategoriesQuery _categories;
    private readonly ISaveCategoryHandler _handler;

    private (string, CategoryKind, string) _saved;

    /// <summary>
    /// Создаёт модель представления карточки группы.
    /// </summary>
    /// <param name="categories">Список категорий: из него берутся подкатегории группы.</param>
    /// <param name="handler">Сохранение категории.</param>
    /// <param name="icons">Набор значков.</param>
    public GroupViewModel(ICategoriesQuery categories, ISaveCategoryHandler handler, IconCatalog icons)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(icons);

        _categories = categories;
        _handler = handler;

        Icon = new IconPicker(icons);
    }

    /// <summary>
    /// Ключ правимой группы. Пусто — заводится новая.
    /// </summary>
    public Guid? Key { get; private set; }

    /// <summary>
    /// Выбор значка группы.
    /// </summary>
    public IconPicker Icon { get; }

    /// <summary>
    /// Подкатегории группы. У новой их нет: сначала группа, потом наполнение.
    /// </summary>
    public ObservableCollection<CategoryRowItem> Subcategories { get; } = [];

    /// <summary>
    /// Подписи видов для переключателя.
    /// </summary>
    public static IReadOnlyList<string> KindNames { get; } = ["Расход", "Доход"];

    /// <summary>
    /// Название группы.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// Вид группы.
    /// </summary>
    [ObservableProperty]
    public partial CategoryKind Kind { get; set; } = CategoryKind.Expense;

    /// <summary>
    /// Группа служебная: в неё нельзя ни заводить, ни переносить.
    /// </summary>
    [ObservableProperty]
    public partial bool IsService { get; private set; }

    /// <summary>
    /// Текст нарушенного правила. Пусто — сохранять можно.
    /// </summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Идёт сохранение. Кнопка зовёт метод напрямую, минуя команду с её защитой
    /// от повторного запуска: без флага второе нажатие до ухода экрана
    /// завело бы вторую группу.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// Сохранять можно: предыдущее сохранение не идёт.
    /// </summary>
    public bool CanSave => !IsSaving;

    /// <summary>
    /// Выбранный вид — номером в переключателе.
    /// </summary>
    public int KindIndex
    {
        get => Array.IndexOf(KindOrder, Kind);
        set => Kind = KindOrder[Math.Clamp(value, 0, KindOrder.Length - 1)];
    }

    /// <summary>
    /// Вид уже заперт: группа существует, и её подкатегории им пользуются.
    /// </summary>
    public bool KindLocked => Key is not null;

    /// <summary>
    /// Вид ещё выбирается.
    /// </summary>
    public bool KindEditable => !KindLocked;

    /// <summary>
    /// Подпись вида, когда он заперт.
    /// </summary>
    public string KindCaption => Kind is CategoryKind.Expense ? "Расход" : "Доход";

    /// <summary>
    /// Заголовок экрана.
    /// </summary>
    public string Title => Key is null ? "Новая группа" : "Группа";

    /// <summary>
    /// Показывать превью «Будет создано»: у существующей группы показывать нечего.
    /// </summary>
    public bool ShowPreview => Key is null;

    /// <summary>
    /// Подпись превью: чем станет заводимая группа.
    /// </summary>
    public string PreviewCaption => Kind is CategoryKind.Expense ? "Группа расходов" : "Группа доходов";

    /// <summary>
    /// Имя приёмника, который заведётся вместе с группой.
    /// </summary>
    public static string ReceiverName => "Прочее";

    /// <summary>
    /// Подкатегорию можно добавить: группа уже существует и не служебная.
    /// </summary>
    public bool CanAddSubcategory => Key is not null && !IsService;

    /// <summary>
    /// Правило нарушено — сообщение показывается рядом с формой.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <inheritdoc />
    public bool IsDirty => Snapshot() != _saved;

    /// <summary>
    /// Правимые поля формы одним значением. Кортеж сравнивается сам, по всем полям
    /// сразу: список «что считать правкой» отдельно от полей разошёлся бы с ними
    /// при первом же новом поле.
    /// </summary>
    private (string Name, CategoryKind Kind, string Icon) Snapshot() => (Name, Kind, Icon.Selected);

    /// <summary>
    /// Загружает группу для правки. Пустой ключ оставляет форму пустой.
    /// </summary>
    /// <param name="key">Ключ группы или <c>null</c> для новой.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(Guid? key, CancellationToken cancellationToken = default)
    {
        if (key is not { } existing)
        {
            _saved = Snapshot();

            return;
        }

        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        IReadOnlyList<CategoryListItem> categories = await _categories.ReadAsync(cancellationToken);

        if (categories.FirstOrDefault(item => item.Key == existing && item.IsGroup) is not { } group)
        {
            _saved = Snapshot();

            return;
        }

        Key = group.Key;
        Name = group.Name;
        Kind = group.Kind;
        IsService = group.Role is CategoryRole.Service;
        Icon.Show(group.Icon);

        Subcategories.Clear();

        foreach (CategoryListItem subcategory in categories.Where(item => item.ParentKey == group.Key))
        {
            Subcategories.Add(CategoryRowItem.From(subcategory));
        }

        _saved = Snapshot();

        Refresh();
    }

    /// <summary>
    /// Сохраняет группу. Нарушенное доменное правило показывается текстом рядом
    /// с формой: это ввод пользователя, а не сбой, и падать приложению не за что.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если группа сохранена и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
        {
            return false;
        }

        IsSaving = true;
        bool done = false;
        Error = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            Key = await _handler.HandleAsync(
                new SaveCategoryCommand
                {
                    Key = Key,
                    ParentKey = null,
                    Name = Name,
                    Icon = Icon.Selected,

                    // Вид читается только при заведении: у существующей группы
                    // обработчик его не меняет, и передавать его незачем
                    Kind = Kind
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
            // в этот промежуток записало бы то же самое ещё раз. При неудаче
            // он снимается — нарушенное правило правят и сохраняют снова
            IsSaving = done;
        }
    }

    partial void OnKindChanged(CategoryKind value)
    {
        OnPropertyChanged(nameof(KindIndex));
        OnPropertyChanged(nameof(KindCaption));
        OnPropertyChanged(nameof(PreviewCaption));
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(KindIndex));
        OnPropertyChanged(nameof(KindLocked));
        OnPropertyChanged(nameof(KindEditable));
        OnPropertyChanged(nameof(KindCaption));
        OnPropertyChanged(nameof(ShowPreview));
        OnPropertyChanged(nameof(PreviewCaption));
        OnPropertyChanged(nameof(CanAddSubcategory));
    }
}
