namespace Finance.Application.Features.Settings.AppLock;

/// <summary>
/// Зачем открыт экран ПИН-кода.
/// </summary>
public enum PinPurpose
{
    /// <summary>
    /// Не задано; настоящим значением не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Задать код и включить защиту: новый код и его повтор.
    /// </summary>
    Create = 1,

    /// <summary>
    /// Сменить код: текущий, затем новый и повтор.
    /// </summary>
    Change = 2,

    /// <summary>
    /// Выключить защиту: только текущий код.
    /// </summary>
    Disable = 3
}
