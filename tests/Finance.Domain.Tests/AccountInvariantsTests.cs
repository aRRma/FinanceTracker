using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Rules;
using Finance.Domain.Values;

namespace Finance.Domain.Tests;

/// <summary>
/// Правила счёта и общие правила имён и удаления, проверенные на счёте и месте.
/// </summary>
public sealed class AccountInvariantsTests
{
    [Fact]
    [Trait("Инвариант", nameof(Invariant.CurrencyFixedOnceUsed))]
    public void Валюта_счёта_с_операциями_не_меняется()
    {
        Account account = Given.Account();

        DomainException error = Assert.Throws<DomainException>(
            () => account.ChangeCurrency(Currency.USD, hasEverHadTransactions: true));

        Assert.Equal(Invariant.CurrencyFixedOnceUsed, error.Invariant);
        Assert.Equal(Currency.RUB, account.Currency);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CurrencyFixedOnceUsed))]
    public void Валюта_счёта_без_операций_меняется()
    {
        Account account = Given.Account(openingBalance: 500m);

        account.ChangeCurrency(Currency.EUR, hasEverHadTransactions: false);

        Assert.Equal(Currency.EUR, account.Currency);
        Assert.Equal(Money.Create(500m, Currency.EUR), account.OpeningBalance);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CurrencyFixedOnceUsed))]
    public void Повторная_установка_той_же_валюты_не_считается_сменой()
    {
        Account account = Given.Account();

        account.ChangeCurrency(Currency.RUB, hasEverHadTransactions: true);

        Assert.Equal(Currency.RUB, account.Currency);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpenedOnNotAfterTransactions))]
    public void Дата_открытия_сдвигается_назад_свободно()
    {
        Account account = Given.Account(openedOn: new DateOnly(2024, 5, 1));

        account.ChangeOpenedOn(new DateOnly(2023, 1, 1), new DateOnly(2024, 6, 1), Given.Today);

        Assert.Equal(new DateOnly(2023, 1, 1), account.OpenedOn);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpenedOnNotAfterTransactions))]
    public void Дата_открытия_не_сдвигается_за_самую_раннюю_операцию()
    {
        Account account = Given.Account(openedOn: new DateOnly(2024, 1, 1));

        DomainException error = Assert.Throws<DomainException>(
            () => account.ChangeOpenedOn(new DateOnly(2024, 7, 1), new DateOnly(2024, 6, 1), Given.Today));

        Assert.Equal(Invariant.OpenedOnNotAfterTransactions, error.Invariant);
        Assert.Equal(new DateOnly(2024, 1, 1), account.OpenedOn);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpenedOnNotAfterTransactions))]
    public void Дата_открытия_сдвигается_вперёд_если_операций_нет()
    {
        Account account = Given.Account(openedOn: new DateOnly(2024, 1, 1));

        account.ChangeOpenedOn(new DateOnly(2025, 1, 1), earliestTransactionOn: null, Given.Today);

        Assert.Equal(new DateOnly(2025, 1, 1), account.OpenedOn);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpeningDateInRange))]
    public void Дата_открытия_в_будущем_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => Given.Account(openedOn: Given.Today.AddDays(1)));

        Assert.Equal(Invariant.OpeningDateInRange, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpeningDateInRange))]
    public void Дата_открытия_раньше_двухтысячного_года_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => Given.Account(openedOn: new DateOnly(1999, 12, 31)));

        Assert.Equal(Invariant.OpeningDateInRange, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpeningDateInRange))]
    public void Дата_открытия_сегодня_принимается()
    {
        Account account = Given.Account(openedOn: Given.Today);

        Assert.Equal(Given.Today, account.OpenedOn);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.OpeningDateInRange))]
    public void Сдвиг_даты_открытия_в_будущее_отвергается()
    {
        Account account = Given.Account(openedOn: new DateOnly(2024, 1, 1));

        DomainException error = Assert.Throws<DomainException>(
            () => account.ChangeOpenedOn(Given.Today.AddDays(1), null, Given.Today));

        Assert.Equal(Invariant.OpeningDateInRange, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AccountDeletedOnlyWithoutTransactions))]
    public void Счёт_с_операциями_не_удаляется()
    {
        Account account = Given.Account();

        DomainException error = Assert.Throws<DomainException>(
            () => account.Delete(Given.NowUtc, hasTransactions: true));

        Assert.Equal(Invariant.AccountDeletedOnlyWithoutTransactions, error.Invariant);
        Assert.False(account.IsDeleted);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AccountDeletedOnlyWithoutTransactions))]
    public void Счёт_без_операций_удаляется()
    {
        Account account = Given.Account();

        account.Delete(Given.NowUtc, hasTransactions: false);

        Assert.Equal(Given.NowUtc, account.DeletedAtUtc);
    }

    /// <summary>
    /// Общий путь удаления сущности не знает об операциях и пропустил бы проверку.
    /// </summary>
    [Fact]
    [Trait("Инвариант", nameof(Invariant.AccountDeletedOnlyWithoutTransactions))]
    public void Счёт_не_удаляется_в_обход_проверки_операций()
    {
        Account account = Given.Account();

        Assert.Throws<InvalidOperationException>(() => account.Delete(Given.NowUtc));
        Assert.False(account.IsDeleted);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.DeletionIsSoft))]
    public void Удаление_только_мягкое_и_повторное_метку_не_сдвигает()
    {
        Place place = Place.Create("Пятёрочка", Given.NowUtc);
        DateTimeOffset first = Given.NowUtc;

        place.Delete(first);
        place.Delete(first.AddDays(1));

        Assert.True(place.IsDeleted);
        Assert.Equal(first, place.DeletedAtUtc);
    }

    /// <summary>
    /// Предел одинаков по обе стороны нуля: начальный остаток бывает и отрицательным.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [Trait("Инвариант", nameof(Invariant.AmountWithinLimit))]
    public void Начальный_остаток_сверх_предела_отвергается(int sign)
    {
        DomainException error = Assert.Throws<DomainException>(
            () => Given.Account(openingBalance: sign * (Money.Limit + 0.01m)));

        Assert.Equal(Invariant.AmountWithinLimit, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AmountWithinLimit))]
    public void Отрицательный_начальный_остаток_до_предела_допустим()
    {
        Account account = Given.Account(openingBalance: -Money.Limit);

        Assert.True(account.OpeningBalance.IsNegative);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameUnique))]
    public void Имя_занятое_соседом_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => NameUniqueness.Ensure("  пятёрочка ", ["Пятёрочка", "Магнит"], RuleText.SubjectPlace));

        Assert.Equal(Invariant.NameUnique, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameUnique))]
    public void Свободное_имя_принимается()
    {
        NameUniqueness.Ensure("Лента", ["Пятёрочка", "Магнит"], RuleText.SubjectPlace);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameUnique))]
    public void При_переименовании_запись_не_конфликтует_сама_с_собой()
    {
        Guid self = Keys.New();
        (Guid, string)[] neighbours = [(self, "Пятёрочка"), (Keys.New(), "Магнит")];

        NameUniqueness.EnsureForRename("Пятёрочка ", neighbours, self, RuleText.SubjectPlace);
    }

    [Theory]
    [Trait("Инвариант", nameof(Invariant.NameTrimmedAndNotEmpty))]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Пустое_имя_отвергается(string? name)
    {
        DomainException error = Assert.Throws<DomainException>(() => Place.Create(name!, Given.NowUtc));

        Assert.Equal(Invariant.NameTrimmedAndNotEmpty, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NameTrimmedAndNotEmpty))]
    public void Имя_хранится_обрезанным()
    {
        Place place = Place.Create("  Пятёрочка  ", Given.NowUtc);

        Assert.Equal("Пятёрочка", place.Name);

        place.Rename("\tМагнит\n");

        Assert.Equal("Магнит", place.Name);
    }

    [Fact]
    public void Восстановление_из_хранилища_ничего_не_проверяет()
    {
        // Читающий путь обязан быть непадающим: значение, вернувшееся из базы
        // с лишним нулём или датой вне диапазона, не должно ронять список счетов
        Account account = Account.Restore(
            Keys.New(), "  Старый счёт  ", AccountType.Cash, AccountColor.Unknown, icon: "  ", Currency.RUB,
            openingBalance: 100.000m, openedOn: new DateOnly(1990, 1, 1),
            excludedFromTotals: false, isClosed: false, sortOrder: 0,
            Given.NowUtc, Given.NowUtc, null, null, null);

        Assert.Equal(100.000m, account.OpeningBalance.Amount);
        Assert.Equal(new DateOnly(1990, 1, 1), account.OpenedOn);
        Assert.Equal(AccountColor.Unknown, account.Color);
        Assert.Equal("  ", account.Icon);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" plane ", "plane")]
    public void Пустой_значок_счёта_значит_значок_по_типу(string? icon, string? expected)
    {
        Account account = Given.Account();

        account.ChangeIcon(icon);

        Assert.Equal(expected, account.Icon);
    }

    [Fact]
    public void Цвет_и_значок_счёта_меняются_без_проверок()
    {
        // Одинаковые цвета и значки у разных счетов разрешены: их различает название
        Account first = Given.Account(name: "Первый");
        Account second = Given.Account(name: "Второй");

        first.ChangeColor(AccountColor.Green);
        second.ChangeColor(AccountColor.Green);
        second.ChangeIcon("plane");

        Assert.Equal(AccountColor.Green, first.Color);
        Assert.Equal(AccountColor.Green, second.Color);
        Assert.Null(first.Icon);
        Assert.Equal("plane", second.Icon);
    }

    [Fact]
    public void Блокировка_счёта_обратима_и_баланс_ей_не_мешает()
    {
        Account account = Given.Account(openingBalance: 12_345m);

        account.Close();
        Assert.True(account.IsClosed);

        account.Reopen();
        Assert.False(account.IsClosed);
    }
}
