using Finance.Application.Features.Accounts.Badge;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;

namespace Finance.Application.Tests;

/// <summary>
/// Очередь цветов и подсказка значка по названию — чистые функции, база им не нужна.
/// </summary>
public sealed class AccountColorsTests
{
    /// <summary>
    /// Первый счёт — синий, дальше — первый свободный по очереди, пропуски заполняются.
    /// </summary>
    [Fact]
    public void Новый_счёт_получает_первый_свободный_цвет()
    {
        Assert.Equal(AccountColor.Blue, AccountColors.Next([]));
        Assert.Equal(AccountColor.Orange, AccountColors.Next([AccountColor.Blue]));
        Assert.Equal(AccountColor.Orange, AccountColors.Next([AccountColor.Blue, AccountColor.Sky]));
    }

    /// <summary>
    /// Заняты все — по кругу, начиная с тех, что повторены меньше других.
    /// </summary>
    [Fact]
    public void Когда_заняты_все_очередь_идёт_по_кругу()
    {
        Assert.Equal(AccountColor.Blue, AccountColors.Next(AccountColors.Queue));
        Assert.Equal(AccountColor.Orange, AccountColors.Next([.. AccountColors.Queue, AccountColor.Blue]));
    }

    /// <summary>
    /// Unknown цвета не занимает: незаполненная строка не сдвигает очередь.
    /// </summary>
    [Fact]
    public void Неизвестный_цвет_очередь_не_сдвигает() =>
        Assert.Equal(AccountColor.Blue, AccountColors.Next([AccountColor.Unknown]));

    /// <summary>
    /// Подсказка находит значки по основе слова в любом месте названия и в любом регистре.
    /// </summary>
    [Theory]
    [InlineData("Кредитка", new[] { "percentage" })]
    [InlineData("Вклад на отпуск", new[] { "building-bank", "coins", "plane" })]
    [InlineData("Доллары дома", new[] { "plane", "coins", "home" })]
    [InlineData("На учёбу", new[] { "school" })]
    [InlineData("На учебу", new[] { "school" })]
    [InlineData("Карта основная", new string[0])]
    public void Подсказка_значка_по_названию(string name, string[] expected) =>
        Assert.Equal(expected, AccountIconHints.For(name));

    /// <summary>
    /// Каждая подсказка — из набора значков счёта: иначе подсказанный значок нечем нарисовать.
    /// </summary>
    [Fact]
    public void Подсказки_берутся_из_набора_значков_счёта()
    {
        string[] names = ["кредит", "вклад", "отпуск", "евро", "дом", "авто", "семья", "подарок", "школа", "резерв", "телефон", "наличные"];

        foreach (string name in names)
        {
            IReadOnlyList<string> hints = AccountIconHints.For(name);

            Assert.NotEmpty(hints);
            Assert.All(hints, static hint => Assert.Contains(hint, AccountIcon.Choices));
        }
    }

    /// <summary>
    /// Значок, выбранный руками, сильнее типа; неизвестный ключ рисуется значком по типу.
    /// </summary>
    [Fact]
    public void Выбранный_значок_сильнее_типа_а_неизвестный_уступает()
    {
        Assert.Equal("plane", AccountIcon.For(AccountType.Card, excludedFromTotals: false, chosen: "plane"));
        Assert.Equal("cash", AccountIcon.For(AccountType.Cash, excludedFromTotals: false, chosen: "no-such-icon"));
    }
}
