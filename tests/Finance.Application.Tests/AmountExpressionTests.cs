using System.Globalization;
using Finance.Application.Infrastructure;

namespace Finance.Application.Tests;

/// <summary>
/// Поле суммы: четыре действия, слева направо, результат до копеек.
/// </summary>
public sealed class AmountExpressionTests
{
    /// <summary>
    /// Обычное число проходит насквозь — выражение это частный случай.
    /// </summary>
    [Theory]
    [InlineData("1250", "1250")]
    [InlineData("1250,50", "1250.50")]
    [InlineData("1250.50", "1250.50")]
    [InlineData("  1250  ", "1250")]
    public void Число_без_действий_читается(string input, string expected)
    {
        Assert.True(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(Parse(expected), result);
    }

    /// <summary>
    /// Четыре действия считаются.
    /// </summary>
    [Theory]
    [InlineData("1250 + 340", "1590")]
    [InlineData("1250-340", "910")]
    [InlineData("120*3", "360")]
    [InlineData("120/4", "30")]
    public void Четыре_действия_считаются(string input, string expected)
    {
        Assert.True(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(Parse(expected), result);
    }

    /// <summary>
    /// Считается слева направо, без приоритета умножения: «1250 + 340 * 2» — это
    /// 3180, а не 1930. Правило намеренное, оно повторяет карманный калькулятор.
    /// </summary>
    [Fact]
    public void Порядок_действий_слева_направо()
    {
        Assert.True(AmountExpression.TryEvaluate("1250 + 340 * 2", out decimal result));
        Assert.Equal(3180m, result);
    }

    /// <summary>
    /// Знаки с клавиатуры формы — типографские, и они тоже понимаются.
    /// </summary>
    [Theory]
    [InlineData("100 × 3", "300")]
    [InlineData("100 ÷ 4", "25")]
    [InlineData("100 − 40", "60")]
    public void Типографские_знаки_понимаются(string input, string expected)
    {
        Assert.True(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(Parse(expected), result);
    }

    /// <summary>
    /// Результат округляется до копеек: точнее копейки домен сумму не примет.
    /// </summary>
    [Theory]
    [InlineData("100/3", "33.33")]
    [InlineData("10/4", "2.50")]
    [InlineData("0,005+0,005", "0.01")]
    public void Результат_округляется_до_копеек(string input, string expected)
    {
        Assert.True(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(Parse(expected), result);
    }

    /// <summary>
    /// Ведущий минус читается: начальный остаток бывает отрицательным — долг
    /// по карте тоже остаток, и ввести его надо как есть.
    /// </summary>
    [Theory]
    [InlineData("-1000,50", "-1000.50")]
    [InlineData("-1000.50", "-1000.50")]
    [InlineData("−250", "-250")]
    [InlineData("-100+40", "-60")]
    public void Ведущий_минус_читается(string input, string expected)
    {
        Assert.True(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(Parse(expected), result);
    }

    /// <summary>
    /// Минус в середине остаётся действием, а не знаком второго числа:
    /// иначе «1250-340» превратилось бы в два числа подряд.
    /// </summary>
    [Fact]
    public void Минус_в_середине_остаётся_действием()
    {
        Assert.True(AmountExpression.TryEvaluate("1250-340", out decimal result));
        Assert.Equal(910m, result);
    }

    /// <summary>
    /// Переполнение не выходит наружу исключением: метод обещает <c>false</c>
    /// на невычислимом вводе, а вызывающий ловит только доменные исключения
    /// и на OverflowException уронил бы приложение.
    /// </summary>
    [Theory]
    [InlineData("79228162514264337593543950335*10")]
    [InlineData("79228162514264337593543950335+79228162514264337593543950335")]
    public void Переполнение_не_бросает_исключение(string input)
    {
        Assert.False(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(0m, result);
    }

    /// <summary>
    /// Незаконченное или бессмысленное выражение результата не даёт. Деление на ноль
    /// здесь не исключение, а ещё не набранное значение: пользователь в процессе ввода.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+")]
    [InlineData("1250+")]
    [InlineData("1250++340")]
    [InlineData("100/0")]
    [InlineData("сто")]
    [InlineData("1,2,3")]
    public void Незаконченное_выражение_результата_не_даёт(string input)
    {
        Assert.False(AmountExpression.TryEvaluate(input, out decimal result));
        Assert.Equal(0m, result);
    }

    // Через строку, а не литералом: decimal-литерал в InlineData компилятор
    // принимает только как double, и 1250.50 доехало бы сюда неточным
    private static decimal Parse(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}
