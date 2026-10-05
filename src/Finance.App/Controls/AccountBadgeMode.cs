namespace Finance.App.Controls;

/// <summary>
/// Где стоит знак счёта — от этого его размер.
/// </summary>
public enum AccountBadgeMode
{
    /// <summary>
    /// Значение неинициализированной переменной; рисуется плашкой.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Плашка в строке списка: балансы, справочник, выбор счёта, поля формы.
    /// </summary>
    Tile = 1,

    /// <summary>
    /// Плашка в шапке ленты счёта и карточки счёта.
    /// </summary>
    Header = 2,

    /// <summary>
    /// Жетон перед названием счёта в подписи строки общей ленты.
    /// </summary>
    Token = 3
}
