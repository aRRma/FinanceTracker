namespace Finance.Domain;

/// <summary>
/// Счёт — место хранения денег в одной валюте. Начальный остаток и дата открытия —
/// поля счёта, а не служебная операция: иначе такая операция требовала бы служебной
/// категории и попадала в ленту и отчёт.
/// </summary>
public sealed class Account : Entity
{
    private Account(
        Guid key,
        string name,
        AccountType type,
        Currency currency,
        Money openingBalance,
        DateOnly openedOn,
        bool excludedFromTotals,
        bool isClosed,
        int sortOrder,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId)
        : base(key, createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId)
    {
        Name = name;
        Type = type;
        Currency = currency;
        OpeningBalance = openingBalance;
        OpenedOn = openedOn;
        ExcludedFromTotals = excludedFromTotals;
        IsClosed = isClosed;
        SortOrder = sortOrder;
    }

    /// <summary>Наименование счёта. Уникально среди неудалённых счетов.</summary>
    public string Name { get; private set; }

    /// <summary>Наличные или карта. Влияет только на значок и подпись.</summary>
    public AccountType Type { get; private set; }

    /// <summary>Валюта счёта. После первой операции не меняется.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Начальный остаток — точка отсчёта баланса. Бывает отрицательным: долг по карте тоже остаток.</summary>
    public Money OpeningBalance { get; private set; }

    /// <summary>Дата, с которой действует начальный остаток. Раньше неё операций по счёту быть не может.</summary>
    public DateOnly OpenedOn { get; private set; }

    /// <summary>«Скрыть из расчётов»: счёт не входит ни в «доступно к тратам», ни в итог дня, ни в отчёт.</summary>
    public bool ExcludedFromTotals { get; private set; }

    /// <summary>
    /// «Счёт закрыт»: выведен из употребления. Обратимо. Даты закрытия нет —
    /// она ничем не используется, а лишнее поле пришлось бы поддерживать при обмене.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>Порядок на главном экране, задаётся перетаскиванием.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Заводит счёт. Уникальность имени проверяется отдельно: она требует списка счетов.</summary>
    /// <param name="name">Наименование счёта.</param>
    /// <param name="type">Наличные или карта.</param>
    /// <param name="currency">Валюта счёта.</param>
    /// <param name="openingBalance">Начальный остаток.</param>
    /// <param name="openedOn">Дата открытия.</param>
    /// <param name="excludedFromTotals">«Скрыть из расчётов».</param>
    /// <param name="sortOrder">Место на главном экране.</param>
    /// <param name="today">Локальная дата пользователя, не дата в UTC.</param>
    /// <param name="nowUtc">Момент создания.</param>
    public static Account Create(
        string name,
        AccountType type,
        Currency currency,
        decimal openingBalance,
        DateOnly openedOn,
        bool excludedFromTotals,
        int sortOrder,
        DateOnly today,
        DateTimeOffset nowUtc)
    {
        Money balance = CreateOpeningBalance(openingBalance, currency);
        Dates.EnsureInRange(openedOn, today, Invariant.OpeningDateInRange, "Дата открытия счёта");

        return new Account(
            Keys.New(),
            Names.Normalize(name, "счёт"),
            type,
            currency,
            balance,
            openedOn,
            excludedFromTotals,
            isClosed: false,
            sortOrder,
            createdAtUtc: nowUtc,
            updatedAtUtc: nowUtc,
            deletedAtUtc: null,
            syncedAtUtc: null,
            externalId: null);
    }

    /// <summary>
    /// ВОССТАНОВЛЕНИЕ ИЗ ХРАНИЛИЩА. Инварианты не проверяются: строка в базе уже
    /// прошла проверку при вводе, а повторная превратила бы чтение в валидацию.
    /// Для создания счёта этот путь не годится — есть <see cref="Create"/>.
    /// </summary>
    public static Account Restore(
        Guid key,
        string name,
        AccountType type,
        Currency currency,
        decimal openingBalance,
        DateOnly openedOn,
        bool excludedFromTotals,
        bool isClosed,
        int sortOrder,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId) =>
        new(key, name, type, currency, Money.Restore(openingBalance, currency), openedOn,
            excludedFromTotals, isClosed, sortOrder,
            createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId);

    /// <summary>Переименовывает счёт.</summary>
    public void Rename(string name) => Name = Names.Normalize(name, "счёт");

    /// <summary>Меняет тип счёта. На расчёты не влияет.</summary>
    public void ChangeType(AccountType type) => Type = type;

    /// <summary>
    /// Меняет валюту счёта. Запрещено, если по счёту когда-либо была операция —
    /// включая мягко удалённые: удалённая операция записана в старой валюте, и смена
    /// валюты переписала бы её задним числом.
    /// </summary>
    /// <param name="currency">Новая валюта.</param>
    /// <param name="hasEverHadTransactions">Существовала ли хоть одна операция по счёту. Читается вызывающей стороной.</param>
    public void ChangeCurrency(Currency currency, bool hasEverHadTransactions)
    {
        if (currency == Currency)
        {
            return;
        }

        DomainException.ThrowIf(
            hasEverHadTransactions,
            Invariant.CurrencyFixedOnceUsed,
            $"Валюта счёта «{Name}» не меняется: по нему уже есть операции");

        Currency = currency;

        // Restore, а не Create: число уже лежало в счёте и проверку при вводе прошло.
        // Create отверг бы значение, вернувшееся из SQLite с лишним нулём, и смена
        // валюты падала бы на проверке точности вместо того, чтобы сработать
        OpeningBalance = Money.Restore(OpeningBalance.Amount, currency);
    }

    /// <summary>Меняет начальный остаток. Правка меняет текущий баланс задним числом и записи в ленте не оставляет.</summary>
    public void ChangeOpeningBalance(decimal openingBalance) =>
        OpeningBalance = CreateOpeningBalance(openingBalance, Currency);

    /// <summary>
    /// Меняет дату открытия. Назад — свободно, вперёд — только если раньше новой
    /// даты нет операций: иначе операция оказалась бы раньше открытия счёта.
    /// </summary>
    /// <param name="openedOn">Новая дата открытия.</param>
    /// <param name="earliestTransactionOn">Дата самой ранней операции по счёту, если операции есть.</param>
    /// <param name="today">Локальная дата пользователя, не дата в UTC.</param>
    public void ChangeOpenedOn(DateOnly openedOn, DateOnly? earliestTransactionOn, DateOnly today)
    {
        Dates.EnsureInRange(openedOn, today, Invariant.OpeningDateInRange, "Дата открытия счёта");

        DomainException.ThrowIf(
            openedOn > OpenedOn && earliestTransactionOn is { } earliest && openedOn > earliest,
            Invariant.OpenedOnNotAfterTransactions,
            $"Дату открытия счёта «{Name}» нельзя сдвинуть на {openedOn:yyyy-MM-dd}: " +
            $"есть операция от {earliestTransactionOn:yyyy-MM-dd}");

        OpenedOn = openedOn;
    }

    /// <summary>Помечает счёт признаком «скрыть из расчётов» или снимает его.</summary>
    public void SetExcludedFromTotals(bool excluded) => ExcludedFromTotals = excluded;

    /// <summary>Закрывает счёт. Ненулевой баланс закрытию не мешает.</summary>
    public void Close() => IsClosed = true;

    /// <summary>Открывает закрытый счёт обратно.</summary>
    public void Reopen() => IsClosed = false;

    /// <summary>Задаёт место счёта на главном экране.</summary>
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>Начальный остаток проходит те же проверки точности и предела, что и сумма операции.</summary>
    private static Money CreateOpeningBalance(decimal amount, Currency currency)
    {
        Money balance = Money.Create(amount, currency);
        balance.EnsureWithinLimit("Начальный остаток");

        return balance;
    }
}
