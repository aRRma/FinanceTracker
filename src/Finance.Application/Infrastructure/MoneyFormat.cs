using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Как сумма выглядит на экране. Собрано в одном месте: разойдись формат между
/// лентой и балансом, одно и то же число выглядело бы разными деньгами.
/// </summary>
public static class MoneyFormat
{
    extension(Currency currency)
    {
        /// <summary>
        /// Знак валюты для подписи суммы.
        /// </summary>
        public string Symbol => currency switch
        {
            Currency.RUB => "₽",
            Currency.USD => "$",
            Currency.EUR => "€",
            _ => currency.ToString()
        };

        /// <summary>
        /// Название валюты как заголовок раздела на главном экране.
        /// </summary>
        public string SectionTitle => currency switch
        {
            Currency.RUB => UiTexts.CurrencyRubles,
            Currency.USD => UiTexts.CurrencyDollars,
            Currency.EUR => UiTexts.CurrencyEuros,
            _ => currency.ToString()
        };
    }

    extension(Money money)
    {
        /// <summary>
        /// Сумма со знаком валюты: <c>82 430,50 ₽</c>.
        /// </summary>
        public string Display => $"{money.Amount.ToString("N", UiCulture.Money)} {money.Currency.Symbol}";

        /// <summary>
        /// Сумма с явным знаком для ленты: доход показан с плюсом, расход с минусом.
        /// Знак берётся у числа, а не у вида операции: вид уже учтён вызывающим.
        /// </summary>
        public string DisplaySigned =>
            money.IsPositive ? $"+{money.Display}" : money.Display;
    }
}
