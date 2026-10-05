using Finance.Application.Texts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Карточка группы: заведение и правка одним экраном. Вид выбирается только при
/// заведении и дальше заперт — его наследуют все подкатегории, и смена вида
/// перевернула бы знак у всей их истории.
/// </summary>
public sealed partial class GroupViewModel : FormViewModel
{
    private readonly ICategoriesQuery _categories;
    private readonly ISaveCategoryHandler _handler;

    private (string Name, CategoryKind Kind, bool AcceptsAnyKind, string Icon) _saved;

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
    /// Группа принимает операции обоих видов, а не только своего. Возврат в магазине
    /// и кэшбэк ложатся в ту же статью, где лежит трата, и в отчёте вычитаются
    /// из неё. Как и вид, выбирается при заведении и потом не меняется.
    /// </summary>
    [ObservableProperty]
    public partial bool AcceptsAnyKind { get; set; }

    /// <summary>
    /// Группа служебная: в неё нельзя ни заводить, ни переносить.
    /// </summary>
    [ObservableProperty]
    public partial bool IsService { get; private set; }

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
    public string KindCaption => Kind is CategoryKind.Expense ? UiTexts.KindExpense : UiTexts.KindIncome;

    /// <summary>
    /// Подпись универсальности: чем группа отличается от односторонней.
    /// </summary>
    public string AcceptsAnyKindCaption => AcceptsAnyKind
        ? UiTexts.GroupAcceptsAnyKind
        : UiTexts.GroupAcceptsOwnKind;

    /// <summary>
    /// Пояснение универсальности показано: при заведении — что даёт переключатель,
    /// у заведённой — только если она и правда универсальна. У односторонней группы
    /// то же пояснение противоречило бы подписи над ним.
    /// </summary>
    public bool AcceptsAnyKindHintVisible => KindEditable || AcceptsAnyKind;

    /// <summary>
    /// Пояснение универсальности словами вида группы: у расходной вычитаются
    /// возвраты, у доходной — расходы. Общая фраза на оба вида была бы неверна
    /// для одного из них.
    /// </summary>
    public string AcceptsAnyKindHint => Kind is CategoryKind.Income
        ? UiTexts.GroupAcceptsAnyKindHintIncome
        : UiTexts.GroupAcceptsAnyKindHint;

    /// <summary>
    /// Заголовок экрана.
    /// </summary>
    public string Title => Key is null ? UiTexts.GroupTitleNew : UiTexts.GroupTitleExisting;

    /// <summary>
    /// Показывать превью «Будет создано»: у существующей группы показывать нечего.
    /// </summary>
    public bool ShowPreview => Key is null;

    /// <summary>
    /// Подпись превью: чем станет заводимая группа.
    /// </summary>
    public string PreviewCaption => Kind is CategoryKind.Expense ? UiTexts.GroupCaptionExpense : UiTexts.GroupCaptionIncome;

    /// <summary>
    /// Имя приёмника, который заведётся вместе с группой.
    /// </summary>
    public static string ReceiverName => UiTexts.CategoryOther;

    /// <summary>
    /// Подкатегорию можно добавить: группа уже существует и не служебная.
    /// </summary>
    public bool CanAddSubcategory => Key is not null && !IsService;

    /// <inheritdoc />
    public override bool IsDirty => Snapshot() != _saved;

    /// <summary>
    /// Правимые поля формы одним значением. Кортеж сравнивается сам, по всем полям
    /// сразу: список «что считать правкой» отдельно от полей разошёлся бы с ними
    /// при первом же новом поле.
    /// </summary>
    private (string Name, CategoryKind Kind, bool AcceptsAnyKind, string Icon) Snapshot() =>
        (Name, Kind, AcceptsAnyKind, Icon.Selected);

    /// <summary>
    /// Загружает группу для правки. Пустой ключ оставляет форму пустой.
    /// </summary>
    /// <param name="key">Ключ группы или <c>null</c> для новой.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
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
        AcceptsAnyKind = group.AcceptsAnyKind;
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
    /// Сохраняет группу.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если группа сохранена и экран можно закрыть.</returns>
    public Task<bool> SaveAsync(CancellationToken cancellationToken = default) =>
        WriteAsync(
            async token =>
            {
                Key = await _handler.HandleAsync(
                    new SaveCategoryCommand
                    {
                        Key = Key,
                        ParentKey = null,
                        Name = Name,
                        Icon = Icon.Selected,

                        // Вид и универсальность читаются только при заведении:
                        // у существующей группы обработчик их не меняет
                        Kind = Kind,
                        AcceptsAnyKind = AcceptsAnyKind
                    },
                    token);

                Refresh();
            },
            cancellationToken);

    partial void OnKindChanged(CategoryKind value)
    {
        OnPropertyChanged(nameof(KindCaption));
        OnPropertyChanged(nameof(PreviewCaption));
        OnPropertyChanged(nameof(AcceptsAnyKindHint));
    }

    partial void OnAcceptsAnyKindChanged(bool value)
    {
        OnPropertyChanged(nameof(AcceptsAnyKindCaption));
        OnPropertyChanged(nameof(AcceptsAnyKindHintVisible));
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(AcceptsAnyKindCaption));
        OnPropertyChanged(nameof(AcceptsAnyKindHintVisible));
        OnPropertyChanged(nameof(KindLocked));
        OnPropertyChanged(nameof(KindEditable));
        OnPropertyChanged(nameof(KindCaption));
        OnPropertyChanged(nameof(ShowPreview));
        OnPropertyChanged(nameof(PreviewCaption));
        OnPropertyChanged(nameof(CanAddSubcategory));
    }
}
