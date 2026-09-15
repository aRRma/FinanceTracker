using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Значок в сетке выбора и то, выбран ли он сейчас. Признак живёт в самой строке,
/// а не считается сравнением в разметке: привязка к равенству двух свойств
/// потребовала бы конвертера, а их в проекте нет.
/// </summary>
public sealed partial class IconChoice : ObservableObject
{
    /// <summary>Создаёт строку сетки.</summary>
    /// <param name="key">Ключ значка из набора.</param>
    public IconChoice(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Key = key;
    }

    /// <summary>Ключ значка.</summary>
    public string Key { get; }

    /// <summary>Значок выбран — в сетке он помечен цветом действия.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
