using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>Чтение списков выбора для формы операции.</summary>
public sealed class TransactionFormQuery : ITransactionFormQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly IAccountsQuery _accounts;
    private readonly ILocalSettings _settings;

    /// <summary>Создаёт запрос.</summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="accounts">Список счетов с балансами — балансы показаны в выборе счёта.</param>
    /// <param name="settings">Локальные настройки: последний использованный счёт.</param>
    public TransactionFormQuery(
        IDbContextFactory<FinanceDbContext> contexts,
        IAccountsQuery accounts,
        ILocalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(settings);

        _contexts = contexts;
        _accounts = accounts;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<TransactionForm> ReadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken).ConfigureAwait(false);

        string? lastAccount = await _settings
            .GetAsync(SettingName.LastAccountKey, cancellationToken)
            .ConfigureAwait(false);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Вид у подкатегории свой не хранится — берётся у группы соединением
        List<CategoryOption> categories = await (
            from subcategory in context.Categories.AsNoTracking()
            join parent in context.Categories.AsNoTracking() on subcategory.ParentKey equals parent.Key
            orderby parent.Kind, parent.Name, subcategory.Name
            select new CategoryOption(subcategory.Key, subcategory.Name, parent.Name, parent.Kind!.Value, subcategory.Icon))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Счётчик — коррелированным подсчётом по индексу места: мест десятки,
        // и по одному подзапросу на место дешевле сводки по всей ленте.
        // Место без операций тоже предлагается, просто в конце списка
        var usage = await context.Places
            .AsNoTracking()
            .Select(place => new
            {
                place.Key,
                place.Name,
                Count = context.Transactions.Count(row => row.PlaceKey == place.Key)
            })
            .OrderByDescending(place => place.Count)
            .ThenBy(place => place.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PlaceOption> places = new(usage.Count);

        foreach (var place in usage)
        {
            places.Add(new PlaceOption(place.Key, place.Name, place.Count));
        }

        List<AccountOption> options = new(accounts.Count);

        foreach (AccountListItem account in accounts)
        {
            options.Add(new AccountOption(
                account.Key,
                account.Name,
                account.Balance.Currency,
                account.Balance,
                account.OpenedOn,
                account.IsClosed));
        }

        return new TransactionForm
        {
            Accounts = options,
            Categories = categories,
            Places = places,
            LastAccountKey = Guid.TryParse(lastAccount, out Guid key) ? key : null
        };
    }
}
