using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// База со стартовым набором и подручные заготовки для тестов операций и ленты:
/// счета, подкатегории обоих видов, команды. Общая для двух наборов тестов,
/// чтобы заведение счёта не повторялось в каждом.
/// </summary>
internal sealed class TransactionFixture : IAsyncDisposable
{
    // Фиксированная, а не из часов: тест не должен зависеть от того,
    // в какую сторону от полуночи по UTC его запустили
    public static readonly DateOnly OpenedOn = new(2026, 1, 1);

    private TransactionFixture(TestDatabase database, Guid expenseCategory, Guid incomeCategory)
    {
        Database = database;
        ExpenseCategory = expenseCategory;
        IncomeCategory = incomeCategory;
    }

    public TestDatabase Database { get; }

    /// <summary>Любая расходная подкатегория стартового набора.</summary>
    public Guid ExpenseCategory { get; }

    /// <summary>Любая доходная подкатегория стартового набора.</summary>
    public Guid IncomeCategory { get; }

    /// <summary>Сегодняшняя дата по часам приложения: дата операции не может быть в будущем.</summary>
    public DateOnly Today => Database.Resolve<Finance.Application.Infrastructure.IClock>().Today;

    public static async Task<TransactionFixture> CreateAsync()
    {
        TestDatabase database = await TestDatabase.CreateAsync();
        await database.Resolve<DatabaseInitializer>().InitializeAsync();

        await using FinanceDbContext context = await database.Contexts.CreateDbContextAsync();

        Guid expense = await SubcategoryAsync(context, CategoryKind.Expense);
        Guid income = await SubcategoryAsync(context, CategoryKind.Income);

        return new TransactionFixture(database, expense, income);
    }

    public Task<Guid> AccountAsync(string name, decimal openingBalance = 0m, Currency currency = Currency.RUB, bool excluded = false) =>
        Database.Resolve<ISaveAccountHandler>().HandleAsync(new SaveAccountCommand
        {
            Name = name,
            Type = AccountType.Card,
            Currency = currency,
            OpeningBalance = openingBalance,
            OpenedOn = OpenedOn,
            ExcludedFromTotals = excluded,
            IsClosed = false
        });

    public async Task CloseAsync(Guid account)
    {
        AccountCard card = (await Database.Resolve<IAccountCardQuery>().ReadAsync(account))!;

        await Database.Resolve<ISaveAccountHandler>().HandleAsync(new SaveAccountCommand
        {
            Key = card.Key,
            Name = card.Name,
            Type = card.Type,
            Currency = card.Currency,
            OpeningBalance = card.OpeningBalance,
            OpenedOn = card.OpenedOn,
            ExcludedFromTotals = card.ExcludedFromTotals,
            IsClosed = true
        });
    }

    public Task<Guid> SaveAsync(SaveTransactionCommand command) =>
        Database.Resolve<ISaveTransactionHandler>().HandleAsync(command);

    public SaveTransactionCommand Expense(Guid account, decimal amount, DateOnly? on = null, string? place = null) =>
        new()
        {
            Kind = TransactionKind.Expense,
            SourceAccountKey = account,
            Amount = amount,
            CategoryKey = ExpenseCategory,
            PlaceName = place,
            OccurredOn = on ?? Today
        };

    public SaveTransactionCommand Income(Guid account, decimal amount, DateOnly? on = null) =>
        new()
        {
            Kind = TransactionKind.Income,
            SourceAccountKey = account,
            Amount = amount,
            CategoryKey = IncomeCategory,
            OccurredOn = on ?? Today
        };

    public SaveTransactionCommand Transfer(Guid from, Guid to, decimal amount, decimal? targetAmount = null, DateOnly? on = null) =>
        new()
        {
            Kind = TransactionKind.Transfer,
            SourceAccountKey = from,
            TargetAccountKey = to,
            Amount = amount,
            TargetAmount = targetAmount,
            OccurredOn = on ?? Today
        };

    public async Task<Money> BalanceAsync(Guid account)
    {
        IReadOnlyList<AccountListItem> accounts = await Database.Resolve<IAccountsQuery>().ReadAsync();

        return accounts.Single(item => item.Key == account).Balance;
    }

    public Task<FeedPage> FeedAsync(Guid? account = null, int skip = 0, int take = 50) =>
        Database.Resolve<IFeedQuery>().ReadAsync(account, skip, take);

    public ValueTask DisposeAsync() => Database.DisposeAsync();

    private static Task<Guid> SubcategoryAsync(FinanceDbContext context, CategoryKind kind) =>
        (from subcategory in context.Categories
         join parent in context.Categories on subcategory.ParentKey equals parent.Key
         where parent.Kind == kind && subcategory.Role == CategoryRole.Normal
         orderby subcategory.Name
         select subcategory.Key)
        .FirstAsync();
}
