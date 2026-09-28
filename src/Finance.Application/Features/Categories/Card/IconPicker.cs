using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Выбор значка из вшитого набора. Общий для карточек группы и подкатегории:
/// поле там одно и то же, и вторая копия разошлась бы с первой.
/// </summary>
public sealed partial class IconPicker : ObservableObject
{
    private readonly IconCatalog _catalog;

    /// <summary>
    /// Создаёт выбор значка.
    /// </summary>
    /// <param name="catalog">Набор значков, вшитый в приложение.</param>
    public IconPicker(IconCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        Choices = [.. catalog.Keys.Select(static key => new IconChoice(key))];
        Selected = catalog.Fallback;

        Mark(Selected);
    }

    /// <summary>
    /// Все значки набора в порядке файла — так они и показываются. Каждый знает,
    /// выбран ли он: без этого в раскрытой сетке не видно, что выбрано сейчас.
    /// </summary>
    public IReadOnlyList<IconChoice> Choices { get; }

    /// <summary>
    /// Выбранный значок.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDescription))]
    public partial string Selected { get; private set; }

    /// <summary>
    /// Что прочтёт озвучка на строке поля: подпись и название выбранного значка.
    /// </summary>
    public string SelectedDescription => string.Format(UiCulture.Current, UiTexts.IconCurrent, IconNames.Of(Selected));

    /// <summary>
    /// Сетка значков раскрыта.
    /// </summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>
    /// Показывает значок категории, подставляя запасной вместо неизвестного.
    /// </summary>
    /// <param name="icon">Ключ значка из категории.</param>
    public void Show(string? icon)
    {
        Selected = _catalog.Resolve(icon);
        IsOpen = false;

        Mark(Selected);
    }

    /// <summary>
    /// Раскрывает и закрывает сетку значков.
    /// </summary>
    [RelayCommand]
    public void Toggle() => IsOpen = !IsOpen;

    /// <summary>
    /// Выбирает значок и закрывает сетку.
    /// </summary>
    /// <param name="icon">Ключ выбранного значка.</param>
    [RelayCommand]
    public void Pick(string icon)
    {
        Selected = _catalog.Resolve(icon);
        IsOpen = false;

        Mark(Selected);
    }

    // Правятся только две строки из восьми десятков — та, что была выбрана,
    // и та, что стала: пересборка коллекции целиком мигала бы всей сеткой
    private void Mark(string key)
    {
        foreach (IconChoice choice in Choices)
        {
            bool selected = string.Equals(choice.Key, key, StringComparison.Ordinal);

            if (choice.IsSelected != selected)
            {
                choice.IsSelected = selected;
            }
        }
    }
}
