namespace Finance.Application.Tests;

/// <summary>
/// Сумма в поле набора, пока запятая не нажата. Подключается через <c>using static</c>.
/// </summary>
internal static class TypedAmount
{
    /// <summary>
    /// Та же сумма, что показывает лента, но без нулей после запятой: так её видит
    /// поле набора, пока пользователь сам не нажал запятую. Сверка идёт от
    /// <c>Display</c>, потому что разряды разделены пробелом, который в тесте не набрать.
    /// </summary>
    /// <param name="display">Сумма из <c>Display</c> или <c>DisplaySigned</c> с целым значением.</param>
    /// <returns>Сумма без «,00».</returns>
    public static string Whole(string display)
    {
        Assert.Contains(",00", display, StringComparison.Ordinal);

        return display.Replace(",00", string.Empty, StringComparison.Ordinal);
    }
}
