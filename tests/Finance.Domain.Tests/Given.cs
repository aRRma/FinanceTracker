namespace Finance.Domain.Tests;

/// <summary>Заготовки для тестов: типовые счёт, категория и операция без повторения параметров.</summary>
internal static class Given
{
    /// <summary>Локальная дата пользователя в тестах. Фиксирована: тест, зависящий от часов, ломается однажды ночью.</summary>
    public static readonly DateOnly Today = new(2026, 9, 12);

    /// <summary>Момент записи в тестах.</summary>
    public static readonly DateTimeOffset NowUtc = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Давняя дата открытия счёта — чтобы не мешала проверкам даты операции.</summary>
    public static readonly DateOnly LongAgo = new(2020, 1, 1);

    public static Account Account(
        string name = "Карта",
        Currency currency = Currency.RUB,
        DateOnly? openedOn = null,
        decimal openingBalance = 0m,
        bool closed = false)
    {
        Account account = Domain.Account.Create(
            name, AccountType.Card, currency, openingBalance, openedOn ?? LongAgo,
            excludedFromTotals: false, sortOrder: 0, Today, NowUtc);

        if (closed)
        {
            account.Close();
        }

        return account;
    }

    public static Category Group(
        string name = "Еда",
        CategoryKind kind = CategoryKind.Expense,
        CategoryRole role = CategoryRole.Normal) =>
        Category.CreateGroup(name, kind, "tools-kitchen-2", NowUtc, role);

    public static Category Subcategory(
        Category parent,
        string name = "Продукты",
        CategoryRole role = CategoryRole.Normal) =>
        Category.CreateSubcategory(parent, name, "shopping-cart", NowUtc, role);

    public static Money Rubles(decimal amount) => Money.Create(amount, Currency.RUB);

    /// <summary>
    /// Разбирает число из строки. Суммы в <c>InlineData</c> задаются строками:
    /// literal вроде <c>-0.01</c> компилятор отдаёт как <c>double</c>, и до decimal
    /// оно доезжает уже неточным.
    /// </summary>
    public static decimal Amount(string value) =>
        decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Расход на переданный счёт и категорию, со значениями по умолчанию для всего остального.</summary>
    public static Transaction Expense(Account account, Category category, decimal amount = 100m) =>
        Transaction.Create(
            TransactionKind.Expense, account.Key, Money.Create(amount, account.Currency),
            null, null, category.Key, null, Today, null, Today, NowUtc);

    /// <summary>
    /// Состояние операции до правки: копия с тем же ключом. Правка меняет операцию
    /// на месте, поэтому прежнее состояние снимается заранее — как это делает
    /// обработчик, прочитавший запись перед изменением.
    /// </summary>
    public static Transaction Snapshot(Transaction transaction) =>
        Transaction.Restore(
            transaction.Key, transaction.Kind, transaction.SourceAccountKey, transaction.TargetAccountKey,
            transaction.Amount, transaction.TargetAmount, transaction.CategoryKey, transaction.PlaceKey,
            transaction.OccurredOn, transaction.Note, transaction.CreatedAtUtc, transaction.UpdatedAtUtc,
            transaction.DeletedAtUtc, transaction.SyncedAtUtc, transaction.ExternalId);

    /// <summary>Перевод между двумя счетами. Сумма зачисления есть всегда, даже в одной валюте.</summary>
    public static Transaction Transfer(
        Account source,
        Account target,
        decimal amount = 100m,
        decimal? targetAmount = null,
        DateOnly? occurredOn = null) =>
        Transaction.Create(
            TransactionKind.Transfer, source.Key, Money.Create(amount, source.Currency),
            target.Key, Money.Create(targetAmount ?? amount, target.Currency),
            null, null, occurredOn ?? Today, null, Today, NowUtc);
}
