using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;

namespace Finance.Domain.Tests;

/// <summary>
/// Тип денег: валюта внутри типа, точность — копейка.
/// </summary>
public sealed class MoneyTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("1.50")]
    [InlineData("-12345.67")]
    [InlineData("999999999999.99")]
    public void Сумма_в_целых_копейках_принимается(string value)
    {
        decimal amount = Given.Amount(value);

        Money money = Money.Create(amount, Currency.RUB);

        Assert.Equal(amount, money.Amount);
        Assert.Equal(Currency.RUB, money.Currency);
    }

    [Theory]
    [Trait("Инвариант", nameof(Invariant.AmountInWholeKopecks))]
    [InlineData("0.001")]
    [InlineData("1.005")]
    [InlineData("1.500")]
    [InlineData("-0.125")]
    public void Сумма_точнее_копейки_отвергается(string value)
    {
        decimal amount = Given.Amount(value);

        DomainException error = Assert.Throws<DomainException>(() => Money.Create(amount, Currency.RUB));

        Assert.Equal(Invariant.AmountInWholeKopecks, error.Invariant);
    }

    [Fact]
    public void Суммы_одной_валюты_складываются()
    {
        Money sum = Money.Create(10.50m, Currency.RUB) + Money.Create(0.50m, Currency.RUB);

        Assert.Equal(Money.Create(11m, Currency.RUB), sum);
    }

    [Fact]
    public void Суммы_одной_валюты_вычитаются()
    {
        Money difference = Money.Create(10m, Currency.USD) - Money.Create(12.25m, Currency.USD);

        Assert.Equal(Money.Create(-2.25m, Currency.USD), difference);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CurrenciesNeverMixed))]
    public void Сложение_разных_валют_бросает_исключение()
    {
        Money rubles = Money.Create(100m, Currency.RUB);
        Money dollars = Money.Create(1m, Currency.USD);

        DomainException error = Assert.Throws<DomainException>(() => rubles + dollars);

        Assert.Equal(Invariant.CurrenciesNeverMixed, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CurrenciesNeverMixed))]
    public void Вычитание_разных_валют_бросает_исключение()
    {
        Money rubles = Money.Create(100m, Currency.RUB);
        Money euros = Money.Create(1m, Currency.EUR);

        DomainException error = Assert.Throws<DomainException>(() => rubles - euros);

        Assert.Equal(Invariant.CurrenciesNeverMixed, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CurrenciesNeverMixed))]
    public void Сравнение_разных_валют_бросает_исключение()
    {
        Money rubles = Money.Create(1m, Currency.RUB);
        Money dollars = Money.Create(100m, Currency.USD);

        DomainException error = Assert.Throws<DomainException>(() => rubles < dollars);

        Assert.Equal(Invariant.CurrenciesNeverMixed, error.Invariant);
    }

    [Fact]
    public void Равные_числа_в_разных_валютах_не_равны()
    {
        Assert.NotEqual(Money.Create(100m, Currency.RUB), Money.Create(100m, Currency.USD));
    }

    [Fact]
    public void Сравнение_в_одной_валюте_упорядочивает_суммы()
    {
        Money smaller = Money.Create(-5m, Currency.RUB);
        Money larger = Money.Create(5m, Currency.RUB);
        Money sameAsLarger = Money.Create(5m, Currency.RUB);

        Assert.True(smaller < larger);
        Assert.True(larger > smaller);
        Assert.True(larger <= sameAsLarger);
        Assert.True(larger >= sameAsLarger);
        Assert.Equal(0, larger.CompareTo(sameAsLarger));
    }

    [Fact]
    public void Отрицательная_сумма_допустима()
    {
        // Баланс уходит в минус штатно и предупреждения не требует
        Money balance = Money.Create(-1500.75m, Currency.RUB);

        Assert.True(balance.IsNegative);
        Assert.False(balance.IsPositive);
    }

    [Fact]
    public void Смена_знака_сохраняет_валюту()
    {
        Money value = -Money.Create(10m, Currency.EUR);

        Assert.Equal(Money.Create(-10m, Currency.EUR), value);
    }

    [Fact]
    public void Ноль_создаётся_в_заданной_валюте()
    {
        Money zero = Money.Zero(Currency.USD);

        Assert.Equal(0m, zero.Amount);
        Assert.Equal(Currency.USD, zero.Currency);
        Assert.False(zero.IsPositive);
        Assert.False(zero.IsNegative);
    }
}
