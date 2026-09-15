using Finance.Application.Features.Categories.Card;

namespace Finance.App.Controls;

/// <summary>
/// Выбор значка категории: строка с выбранным и раскрывающаяся сетка набора.
/// Привязывается к <see cref="IconPicker"/> карточки.
/// </summary>
public partial class IconGrid : ContentView
{
    /// <summary>Создаёт контрол.</summary>
    public IconGrid()
    {
        InitializeComponent();
    }

    private void OnIconTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: IconChoice choice }
            && BindingContext is IconPicker picker)
        {
            picker.Pick(choice.Key);
        }
    }
}
