using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Строка списка категорий: и шапка группы, и подкатегория под ней, и подпись
/// раздела. Один тип на все, потому что список плоский: группировка списка в MAUI
/// прячет заголовок вместе с содержимым, а свёрнутой группе заголовок как раз и нужен.
/// </summary>
public sealed partial class CategoryPickerLine : ObservableObject
{
    /// <summary>
    /// Создаёт строку списка.
    /// </summary>
    /// <param name="key">Ключ категории.</param>
    /// <param name="icon">Ключ значка.</param>
    /// <param name="name">Название.</param>
    /// <param name="isGroup">Строка — шапка группы: разворачивает, а с одной подкатегорией выбирает её.</param>
    /// <param name="count">Число подкатегорий — у шапки группы.</param>
    /// <param name="isProtected">Приёмник или служебная: показывается приглушённой.</param>
    /// <param name="isSelected">Эта подкатегория и стоит в форме сейчас.</param>
    public CategoryPickerLine(
        Guid key,
        string icon,
        string name,
        bool isGroup,
        int count,
        bool isProtected,
        bool isSelected)
    {
        Key = key;
        Icon = icon;
        Name = name;
        IsGroup = isGroup;
        Count = count;
        IsProtected = isProtected;
        IsSelected = isSelected;
    }

    /// <summary>
    /// Подпись раздела: отделяет группы другого вида, принимающие операцию как
    /// возврат, от групп вида самой операции. Не выбирается и не разворачивается.
    /// </summary>
    /// <param name="title">Текст подписи.</param>
    /// <returns>Строка-подпись.</returns>
    public static CategoryPickerLine Section(string title) =>
        new(Guid.Empty, string.Empty, title, isGroup: false, count: 0, isProtected: false, isSelected: false)
        {
            IsSection = true
        };

    /// <summary>
    /// Ключ категории.
    /// </summary>
    public Guid Key { get; }

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Название.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Строка — шапка группы.
    /// </summary>
    public bool IsGroup { get; }

    /// <summary>
    /// Строка — подпись раздела, а не категория.
    /// </summary>
    public bool IsSection { get; private init; }

    /// <summary>
    /// Строка — категория: шапка группы или подкатегория.
    /// </summary>
    public bool IsCategory => !IsSection;

    /// <summary>
    /// Строка — подкатегория: она с отступом и её можно выбрать.
    /// </summary>
    public bool IsSubcategory => !IsGroup && !IsSection;

    /// <summary>
    /// Единственная подкатегория группы. Такая группа не разворачивается, а выбирается
    /// сама: раскрывать ради одной строки — лишнее касание.
    /// </summary>
    public Guid? Only { get; init; }

    /// <summary>
    /// Что уйдёт в форму по касанию строки: подкатегория или единственная подкатегория
    /// группы. У разворачиваемой группы и подписи раздела — ничего.
    /// </summary>
    public Guid? Choice => IsSubcategory ? Key : Only;

    /// <summary>
    /// Шапка группы, которая разворачивается: у неё стрелка и число подкатегорий.
    /// </summary>
    public bool IsExpandable => IsGroup && Only is null;

    /// <summary>
    /// Число подкатегорий в группе — плашкой у шапки.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Приёмник или служебная категория — приглушена.
    /// </summary>
    public bool IsProtected { get; }

    /// <summary>
    /// Выбранная сейчас подкатегория — помечена галочкой. У группы из одной
    /// подкатегории помечена сама шапка: раскрывать её нечего.
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>
    /// Группа развёрнута — под шапкой видны её подкатегории.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronIcon))]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    /// Знак состояния группы: вниз — развёрнута, вправо — свёрнута.
    /// </summary>
    public string ChevronIcon => IsExpanded ? "chevron-down" : "chevron-right";
}
