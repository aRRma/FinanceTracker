using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Выбор значка из вшитого набора. Общий для карточек группы и подкатегории:
/// поле там одно и то же, и вторая копия разошлась бы с первой.
/// </summary>
public sealed partial class IconPicker : ObservableObject
{
    private readonly IconCatalog _catalog;

    /// <summary>Создаёт выбор значка.</summary>
    /// <param name="catalog">Набор значков, вшитый в приложение.</param>
    public IconPicker(IconCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        Selected = catalog.Fallback;
    }

    /// <summary>Все значки набора в порядке файла — так они и показываются.</summary>
    public IReadOnlyList<string> Keys => _catalog.Keys;

    /// <summary>Выбранный значок.</summary>
    [ObservableProperty]
    public partial string Selected { get; private set; }

    /// <summary>Сетка значков раскрыта.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Показывает значок категории, подставляя запасной вместо неизвестного.</summary>
    /// <param name="icon">Ключ значка из категории.</param>
    public void Show(string? icon)
    {
        Selected = _catalog.Resolve(icon);
        IsOpen = false;
    }

    /// <summary>Раскрывает и закрывает сетку значков.</summary>
    [RelayCommand]
    public void Toggle() => IsOpen = !IsOpen;

    /// <summary>Выбирает значок и закрывает сетку.</summary>
    /// <param name="icon">Ключ выбранного значка.</param>
    [RelayCommand]
    public void Pick(string icon)
    {
        Selected = _catalog.Resolve(icon);
        IsOpen = false;
    }
}
