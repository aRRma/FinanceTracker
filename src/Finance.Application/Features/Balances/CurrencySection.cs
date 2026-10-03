namespace Finance.Application.Features.Balances;

/// <summary>
/// Раздел одной валюты на главном экране. Подытог в разделе не больше одного —
/// «доступно к тратам»: общей суммы по валютам не существует, а накопления
/// не показывают суммой намеренно, иначе «скрытый» теряет смысл.
/// </summary>
/// <param name="Title">Заголовок раздела: «Рубли», «Евро».</param>
/// <param name="Available">Доступно к тратам, уже отформатировано.</param>
/// <param name="IsAvailableNegative">Доступно к тратам ушло в минус: подытог показывают смысловым цветом.</param>
/// <param name="Spendable">Счета, деньги которых пользователь считает тратимыми.</param>
/// <param name="Savings">Накопления — счета с признаком «скрытый».</param>
public sealed record CurrencySection(
    string Title,
    string Available,
    bool IsAvailableNegative,
    IReadOnlyList<AccountTile> Spendable,
    IReadOnlyList<AccountTile> Savings)
{
    /// <summary>
    /// В валюте есть нескрытые счета. Без них «доступно» всегда ноль
    /// и ничего не говорит, а пустая карточка выглядит поломкой.
    /// </summary>
    public bool HasSpendable => Spendable.Count > 0;

    /// <summary>
    /// В разделе есть накопления — заголовок «Накопления» показывать стоит.
    /// </summary>
    public bool HasSavings => Savings.Count > 0;
}
