using System.Globalization;
using Finance.Domain.Enums;
using Finance.Domain.Errors;

namespace Finance.Domain.Values;

/// <summary>
/// Денежная сумма: число с фиксированной точностью и валюта внутри одного типа.
/// Валюта внутри, а не рядом, потому что общей суммы по валютам в этом приложении
/// не существует ни на одном экране — попытка сложить разные валюты обязана падать
/// на этапе типа, а не давать неверное число.
/// </summary>
/// <remarks>
/// Обычная <c>readonly struct</c>, а не <c>record struct</c>: выражение <c>with</c>
/// собрало бы сумму в обход проверки точности в <see cref="Create"/>.
/// </remarks>
public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    /// <summary>
    /// Копейки — предел точности: всё, что мельче, в этой предметной области не существует.
    /// </summary>
    private const int MaxScale = 2;

    /// <summary>
    /// Предел суммы по модулю. Ровно столько помещается в колонку
    /// <c>numeric(19,2)</c> с запасом, и на столько же рассчитана клавиатура ввода.
    /// </summary>
    public const decimal Limit = 999_999_999_999.99m;

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Число без валюты. Знак допустим: баланс бывает отрицательным.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Валюта суммы.
    /// </summary>
    public Currency Currency { get; }

    /// <summary>
    /// Сумма положительна.
    /// </summary>
    public bool IsPositive => Amount > 0m;

    /// <summary>
    /// Сумма отрицательна.
    /// </summary>
    public bool IsNegative => Amount < 0m;

    /// <summary>
    /// Создаёт сумму, проверяя, что она выражена в целых копейках. Считаются записанные
    /// знаки, а не значение: <c>1.500</c> отвергается наравне с <c>1.505</c>.
    /// </summary>
    /// <exception cref="DomainException">Больше двух знаков после запятой.</exception>
    public static Money Create(decimal amount, Currency currency)
    {
        // Округлить молча нельзя: округление — решение формы ввода,
        // и домен, делающий это сам, скроет ошибку вызывающего кода
        DomainException.ThrowIf(
            amount.Scale > MaxScale,
            Invariant.AmountInWholeKopecks,
            $"Сумма {amount} точнее копейки: {amount.Scale} знаков после запятой при допустимых {MaxScale}");

        return new Money(amount, currency);
    }

    /// <summary>
    /// ВОССТАНОВЛЕНИЕ ИЗ ХРАНИЛИЩА. Точность не проверяется: число в базе уже прошло
    /// проверку при вводе, а повторная сделала бы чтение способным упасть — значение,
    /// вернувшееся из SQLite с лишним нулём, ронял бы весь список счетов.
    /// Для ввода этот путь не годится — есть <see cref="Create"/>.
    /// </summary>
    public static Money Restore(decimal amount, Currency currency) => new(amount, currency);

    /// <summary>
    /// Нулевая сумма в указанной валюте. Точка отсчёта для накопления итогов.
    /// </summary>
    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>
    /// Проверяет предел суммы. Живёт здесь, а не у каждого поля: предел один,
    /// и записанный в трёх местах он разойдётся при первой же правке.
    /// Баланс через эту проверку не проходит — предел задан сумме операции.
    /// </summary>
    /// <param name="what">Что проверяется — попадёт в текст ошибки.</param>
    public void EnsureWithinLimit(string what) =>
        DomainException.ThrowIf(
            Math.Abs(Amount) > Limit,
            Invariant.AmountWithinLimit,
            $"{what} {this} превышает предел {Limit}");

    /// <summary>
    /// Складывает суммы одной валюты.
    /// </summary>
    /// <exception cref="DomainException">Валюты различаются.</exception>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "сложить");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>
    /// Вычитает суммы одной валюты.
    /// </summary>
    /// <exception cref="DomainException">Валюты различаются.</exception>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "вычесть");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>
    /// Меняет знак суммы. Валюта сохраняется.
    /// </summary>
    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    /// <summary>
    /// Суммы равны, если совпали и число, и валюта.
    /// </summary>
    public static bool operator ==(Money left, Money right) => left.Equals(right);

    /// <summary>
    /// Суммы различаются числом или валютой.
    /// </summary>
    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    /// <summary>
    /// Левая сумма меньше правой.
    /// </summary>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Левая сумма больше правой.
    /// </summary>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Левая сумма не больше правой.
    /// </summary>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Левая сумма не меньше правой.
    /// </summary>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Сравнивает суммы одной валюты.
    /// </summary>
    /// <exception cref="DomainException">Валюты различаются.</exception>
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other, "сравнить");
        return Amount.CompareTo(other.Amount);
    }

    /// <summary>
    /// Суммы равны, если совпали и число, и валюта.
    /// </summary>
    public bool Equals(Money other) => Currency == other.Currency && Amount == other.Amount;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    /// <summary>
    /// Представление для журналов и сообщений об ошибках, не для интерфейса.
    /// </summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    /// <summary>
    /// Валюты обязаны совпадать. Разные валюты — не «особый случай, который бывает»,
    /// а ошибка в коде: сложение рублей с долларами не имеет смысла ни при каком курсе.
    /// </summary>
    private static void EnsureSameCurrency(Money left, Money right, string action) =>
        DomainException.ThrowIf(
            left.Currency != right.Currency,
            Invariant.CurrenciesNeverMixed,
            $"Нельзя {action} суммы в разных валютах: {left.Currency} и {right.Currency}");
}
