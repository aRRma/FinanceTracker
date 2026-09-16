using System.Globalization;
using Finance.Application.Infrastructure;

namespace Finance.Application.Tests;

/// <summary>
/// Набор суммы с клавиатуры формы. Невозможное нажатие ничего не меняет:
/// промах пальцем не повод ругаться на пользователя.
/// </summary>
public sealed class AmountInputTests
{
    /// <summary>Цифры набираются подряд, а ведущий ноль заменяется: «05» никто не имеет в виду.</summary>
    [Theory]
    [InlineData("", '1', "1")]
    [InlineData("12", '5', "125")]
    [InlineData("0", '7', "7")]
    [InlineData("10", '0', "100")]
    [InlineData("1+0", '5', "1+5")]
    public void Цифра_дописывается_к_числу(string expression, char key, string expected) =>
        Assert.Equal(expected, AmountInput.Append(expression, key));

    /// <summary>
    /// Предел суммы — двенадцать разрядов до запятой и две копейки после,
    /// тот же, что у <c>Invariant.AmountWithinLimit</c>.
    /// Лишний разряд клавиатура не принимает: набрать и получить отказ при
    /// сохранении хуже, чем не набрать.
    /// </summary>
    [Theory]
    [InlineData("999999999999", '9', "999999999999")]
    [InlineData("99999999999", '9', "999999999999")]
    [InlineData("12,34", '5', "12,34")]
    [InlineData("12,3", '4', "12,34")]
    public void Лишние_разряды_не_набираются(string expression, char key, string expected) =>
        Assert.Equal(expected, AmountInput.Append(expression, key));

    /// <summary>Предел считается для каждого числа выражения отдельно, а не для всей строки.</summary>
    [Fact]
    public void Предел_разрядов_у_каждого_числа_свой() =>
        Assert.Equal("999999999999+1", AmountInput.Append("999999999999+", '1'));

    /// <summary>Запятая одна на число и не первая: целая часть обязана быть.</summary>
    [Theory]
    [InlineData("", "0,")]
    [InlineData("12", "12,")]
    [InlineData("12,3", "12,3")]
    [InlineData("1+", "1+0,")]
    public void Запятая_одна_на_число(string expression, string expected) =>
        Assert.Equal(expected, AmountInput.Append(expression, ','));

    /// <summary>
    /// Знак действия — только после числа, и второй подряд заменяет первый:
    /// пользователь передумал, а не хочет два действия подряд.
    /// </summary>
    [Theory]
    [InlineData("", '+', "")]
    [InlineData("12", '+', "12+")]
    [InlineData("12+", '×', "12×")]
    [InlineData("12,", '−', "12−")]
    public void Действие_ставится_только_после_числа(string expression, char key, string expected) =>
        Assert.Equal(expected, AmountInput.Append(expression, key));

    /// <summary>Стирание убирает последний знак; пустое поле стирать нечем.</summary>
    [Theory]
    [InlineData("125", "12")]
    [InlineData("12+", "12")]
    [InlineData("", "")]
    public void Стирание_убирает_последний_знак(string expression, string expected) =>
        Assert.Equal(expected, AmountInput.Backspace(expression));

    /// <summary>
    /// «=» сворачивает выражение в число, и набор с него продолжается: итог
    /// возвращается в поле теми же знаками, какими его набирают — без разделителей
    /// разрядов и без хвоста нулей.
    /// </summary>
    [Theory]
    [InlineData("1250+340", "1590")]
    [InlineData("1250×1000", "1250000")]
    [InlineData("10÷4", "2,5")]
    [InlineData("10÷3", "3,33")]
    [InlineData("1250", "1250")]
    [InlineData("12,50", "12,5")]
    public void Равно_сворачивает_выражение_в_число(string expression, string expected) =>
        Assert.Equal(expected, AmountInput.Collapse(expression));

    /// <summary>Считать нечего — выражение остаётся как набрано, а не обнуляется.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("12+")]
    [InlineData("12÷0")]
    public void Незаконченное_выражение_не_сворачивается(string expression) =>
        Assert.Equal(expression, AmountInput.Collapse(expression));

    /// <summary>
    /// Незакрытое действие видно по самому выражению: по нему экран и решает,
    /// показывать ли итог.
    /// </summary>
    [Theory]
    [InlineData("1250", false)]
    [InlineData("12,50", false)]
    [InlineData("1250+340", true)]
    [InlineData("1250+", true)]
    public void Незакрытое_действие_видно_в_выражении(string expression, bool expected) =>
        Assert.Equal(expected, AmountInput.HasOperation(expression));

    /// <summary>
    /// Набранное с клавиатуры вычисляется тем же разборщиком, что и введённое
    /// с внешней: знаки действий у них разные, и разойтись им нельзя.
    /// </summary>
    [Fact]
    public void Набранное_с_клавиатуры_вычисляется()
    {
        string typed = string.Empty;

        foreach (char key in "1250+340")
        {
            typed = AmountInput.Append(typed, key is '+' ? '+' : key);
        }

        Assert.True(AmountExpression.TryEvaluate(typed, out decimal value));
        Assert.Equal(1590m, value);
    }

    /// <summary>Клавиши, которой нет на клавиатуре, быть не может: это ошибка разметки, а не ввода.</summary>
    [Fact]
    public void Незнакомая_клавиша_отвергается() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AmountInput.Append("12", 'x'));

    /// <summary>
    /// Разделитель — знак культуры устройства, а не жёсткая запятая. На английской
    /// локали набирается точка: иначе сумма показывалась бы с точкой, а набрать её
    /// было бы нечем. Культура ставится вокруг вызова — она читается на каждом нажатии.
    /// </summary>
    [Theory]
    [InlineData("ru-RU", ',', "12,")]
    [InlineData("en-US", '.', "12.")]
    public void Разделитель_берётся_из_культуры(string culture, char key, string expected)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            Assert.Equal(key, AmountInput.Separator);
            Assert.Equal(expected, AmountInput.Append("12", key));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
