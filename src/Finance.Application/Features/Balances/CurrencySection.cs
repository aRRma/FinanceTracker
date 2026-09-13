namespace Finance.Application.Features.Balances;

/// <summary>
/// Раздел одной валюты на главном экране. Подытог в разделе ровно один —
/// «доступно к тратам»: общей суммы по валютам не существует, а накопления
/// не показывают суммой намеренно, иначе «скрыть из расчётов» теряет смысл.
/// </summary>
/// <param name="Title">Заголовок раздела: «Рубли», «Евро».</param>
/// <param name="Available">Доступно к тратам, уже отформатировано.</param>
/// <param name="Spendable">Счета, деньги которых пользователь считает тратимыми.</param>
/// <param name="Savings">Накопления — счета со «скрыть из расчётов».</param>
public sealed record CurrencySection(
    string Title,
    string Available,
    IReadOnlyList<AccountTile> Spendable,
    IReadOnlyList<AccountTile> Savings)
{
    /// <summary>В разделе есть накопления — заголовок «Накопления» показывать стоит.</summary>
    public bool HasSavings => Savings.Count > 0;
}
