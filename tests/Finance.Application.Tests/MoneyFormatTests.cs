using Finance.Application.Infrastructure;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Вид суммы на экране. Остальные тесты строят ожидание через тот же <c>Display</c>,
/// и поломку самого формата не заметили бы: здесь он сверяется с разрядами культуры,
/// неразрывным пробелом перед знаком валюты и плюсом у дохода.
/// </summary>
public sealed class MoneyFormatTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("-0.01")]
    [InlineData("1500.5")]
    [InlineData("-1350.99")]
    [InlineData("1234567.89")]
    public void Сумма_показывается_с_разрядами_и_знаком_валюты(string amount)
    {
        decimal value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        foreach (Currency currency in (Currency[])[Currency.RUB, Currency.USD, Currency.EUR])
        {
            Money money = Money.Restore(value, currency);
            string expected = value.ToString("N", UiCulture.Money) + " " + currency.Symbol;

            Assert.Equal(expected, money.Display);
            Assert.Equal(value > 0 ? "+" + expected : expected, money.DisplaySigned);
        }
    }
}
