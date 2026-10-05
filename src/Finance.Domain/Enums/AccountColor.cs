namespace Finance.Domain.Enums;

/// <summary>
/// Цвет счёта — чтобы узнать счёт с первого взгляда. Номер члена не задаёт очередь,
/// в которой цвет достаётся новому счёту: очередь — забота прикладного слоя.
/// </summary>
public enum AccountColor
{
    /// <summary>
    /// Значение неинициализированной переменной. Настоящим цветом счёта не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Синий.
    /// </summary>
    Blue = 1,

    /// <summary>
    /// Оранжевый.
    /// </summary>
    Orange = 2,

    /// <summary>
    /// Голубой.
    /// </summary>
    Sky = 3,

    /// <summary>
    /// Зелёный.
    /// </summary>
    Green = 4,

    /// <summary>
    /// Фиолетовый.
    /// </summary>
    Violet = 5,

    /// <summary>
    /// Пурпурный.
    /// </summary>
    Magenta = 6,

    /// <summary>
    /// Красный.
    /// </summary>
    Red = 7,

    /// <summary>
    /// Розовый.
    /// </summary>
    Pink = 8
}
