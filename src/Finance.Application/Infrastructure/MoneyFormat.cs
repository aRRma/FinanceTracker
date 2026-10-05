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
    /// <summary>
    /// Набираемое число без знака валюты: так форма операции показывает сумму, пока
    /// счёт не выбран и валюты ещё нет. Дробная часть — только после набранной запятой.
    /// </summary>
    /// <param name="amount">Итог набранного.</param>
    /// <param name="typed">Набранное на клавиатуре суммы, без знака действия.</param>
    /// <returns>Число с разрядами: <c>1 500</c>, а с запятой — <c>1 500,50</c>.</returns>
    public static string Typed(decimal amount, string typed) => amount.ToString(TypedFormat(typed), UiCulture.Money);

    /// <summary>
    /// Нули после запятой, которую никто не набирал, — шум: пока пользователь
    /// не нажал запятую, сумма целая, и «,00» к ней ничего не добавляет.
    /// </summary>
    private static string TypedFormat(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);

        return typed.Contains(AmountInput.Separator, StringComparison.Ordinal) ? "N" : "N0";
    }

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
        public string Display => string.Create(UiCulture.Money, $"{money.Amount:N} {money.Currency.Symbol}");

        /// <summary>
        /// Набираемая сумма со знаком валюты: <c>1 500 ₽</c>, а после запятой — <c>1 500,50 ₽</c>.
        /// </summary>
        /// <param name="typed">Набранное на клавиатуре суммы, без знака действия.</param>
        /// <returns>Сумма для поля, которое набирают клавиатурой суммы.</returns>
        public string DisplayTyped(string typed) => $"{Typed(money.Amount, typed)} {money.Currency.Symbol}";

        /// <summary>
        /// Сумма с явным знаком для ленты: доход показан с плюсом, расход с минусом.
        /// Знак берётся у числа, а не у вида операции: вид уже учтён вызывающим.
        /// </summary>
        public string DisplaySigned =>
            money.IsPositive ? $"+{money.Display}" : money.Display;
    }
}
