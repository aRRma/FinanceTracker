using Finance.Application.Features.Accounts.Card;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Заготовки для тестов счетов на пустой базе, без стартового набора: команда счёта
/// и операции, записанные мимо обработчика. Подключается через <c>using static</c>.
/// </summary>
internal static class AccountSetup
{
    /// <summary>
    /// Дата открытия счёта — заведомо раньше любой операции тестов.
    /// </summary>
    public static readonly DateOnly OpenedOn = new(2026, 1, 1);

    /// <summary>
    /// Дата операций, записанных мимо обработчика. Фиксированная, а не из часов:
    /// тест не должен зависеть от того, в какую сторону от полуночи по UTC его запустили.
    /// </summary>
    public static readonly DateOnly Today = new(2026, 8, 25);

    /// <summary>
    /// Метка создания и удаления таких операций. В проверках она не участвует,
    /// но и не берётся из часов: исход теста не зависит от момента запуска.
    /// </summary>
    public static readonly DateTimeOffset NowUtc = new(2026, 8, 25, 9, 30, 0, TimeSpan.Zero);

    public static SaveAccountCommand Command(string name, decimal openingBalance = 0m, bool excludedFromTotals = false) =>
        new()
        {
            Name = name,
            Type = AccountType.Cash,
            Color = AccountColor.Blue,
            Currency = Currency.RUB,
            OpeningBalance = openingBalance,
            OpenedOn = OpenedOn,
            ExcludedFromTotals = excludedFromTotals,
            IsClosed = false
        };

    public static Task<Guid> SaveAsync(TestDatabase database, SaveAccountCommand command) =>
        database.Resolve<ISaveAccountHandler>().HandleAsync(command);

    /// <summary>
    /// Заводит счёт и блокирует его правкой — как пользователь: при заведении
    /// признак блокировки не действует.
    /// </summary>
    public static async Task<Guid> SaveClosedAsync(TestDatabase database, SaveAccountCommand command)
    {
        Guid key = await SaveAsync(database, command);

        await SaveAsync(database, command with { Key = key, IsClosed = true });

        return key;
    }

    /// <summary>
    /// Записывает операцию прямо в базу. Категория у такой операции выдуманная:
    /// стартового набора в базе нет, а балансу и ленте категория не нужна.
    /// </summary>
    public static async Task AddAsync(TestDatabase database, Transaction transaction)
    {
        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        context.Transactions.Add(transaction.ToRow());

        await context.SaveChangesAsync();
    }

    public static Transaction Expense(Guid account, decimal amount) =>
        Transaction.Create(
            TransactionKind.Expense, account, Money.Create(amount, Currency.RUB),
            targetAccountKey: null, targetAmount: null, categoryKey: Guid.CreateVersion7(),
            placeKey: null, occurredOn: Today, note: null, Today, NowUtc);

    public static Transaction Income(Guid account, decimal amount) =>
        Transaction.Create(
            TransactionKind.Income, account, Money.Create(amount, Currency.RUB),
            targetAccountKey: null, targetAmount: null, categoryKey: Guid.CreateVersion7(),
            placeKey: null, occurredOn: Today, note: null, Today, NowUtc);

    public static Transaction Transfer(Guid from, Guid to, decimal amount) =>
        Transaction.Create(
            TransactionKind.Transfer, from, Money.Create(amount, Currency.RUB),
            targetAccountKey: to, targetAmount: Money.Create(amount, Currency.RUB), categoryKey: null,
            placeKey: null, occurredOn: Today, note: null, Today, NowUtc);
}
