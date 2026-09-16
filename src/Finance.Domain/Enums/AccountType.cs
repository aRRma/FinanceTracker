namespace Finance.Domain.Enums;

/// <summary>
/// Тип счёта. Влияет только на значок и подпись, на расчёты — нет.
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Значение неинициализированной переменной. Настоящим типом счёта не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Наличные.
    /// </summary>
    Cash = 1,

    /// <summary>
    /// Карта.
    /// </summary>
    Card = 2
}
