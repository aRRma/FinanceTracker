namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Чем кончилась проверка набранного ПИН-кода.
/// </summary>
public enum PinCheckOutcome
{
    /// <summary>
    /// Не задано; настоящим значением не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Код верный.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// Код неверный, попытка засчитана.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Идёт пауза после неверных попыток: код не проверялся и попыткой не считается.
    /// </summary>
    Paused = 3
}
