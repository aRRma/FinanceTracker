using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Errors;
using Finance.Domain.Rules;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Import.Wallet;

/// <summary>
/// Переносит историю из Wallet тем же пишущим путём, что и формы: счёт, место
/// и операция создаются доменными фабриками, операция проходит <see cref="TransactionRules"/>.
/// Отдельного «доверенного» пути нет намеренно — перенос восьми лет в обход правил
/// оставил бы в базе записи, которые форма потом не смогла бы сохранить при правке.
/// </summary>
/// <remarks>
/// Работает на рабочей машине, а не в приложении: перенос пишет историю в свежую
/// базу, и клиент открывает её восстановлением из файла. Нарушенное правило называет
/// себя своим текстом, но без номера записи; номер есть у ссылки мимо файла.
/// </remarks>
public sealed class WalletImportHandler : IWalletImportHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="contexts">Фабрика контекстов — для проверки, пуста ли база.</param>
    /// <param name="clock">Часы: «сегодня» пользователя и момент записи счетов и мест.</param>
    public WalletImportHandler(UnitOfWork unitOfWork, IDbContextFactory<FinanceDbContext> contexts, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(clock);

        _unitOfWork = unitOfWork;
        _contexts = contexts;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await IsEmptyAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<WalletImportCounts> HandleAsync(WalletImportFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _unitOfWork.ExecuteAsync<WalletImportCounts>(
            async (context, token) =>
            {
                // Проверка внутри той же транзакции, а не только перед нажатием:
                // второй перенос поверх первого удвоил бы все балансы
                if (!await IsEmptyAsync(context, token).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(ImportFaults.WalletImportNotEmpty());
                }

                DateOnly today = _clock.Today;
                DateTimeOffset now = _clock.NowUtc;

                Dictionary<string, Account> accounts = AddAccounts(context, file.Accounts, today, now);
                Dictionary<string, Guid> places = AddPlaces(context, file.Places, now);
                (Dictionary<string, Category> byTextKey, Dictionary<Guid, Category> byKey) =
                    await ReadCategoriesAsync(context, token).ConfigureAwait(false);

                for (int index = 0; index < file.Transactions.Count; index++)
                {
                    context.Transactions.Add(
                        BuildTransaction(file.Transactions[index], index, accounts, places, byTextKey, byKey, today).ToRow());
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return (WalletImportCounts.Of(file), DataChange.Accounts | DataChange.Places | DataChange.Transactions);
            },
            cancellationToken);
    }

    /// <summary>
    /// Пуста ли база. Удалённые записи тоже считаются: перенос в базу, где счёт
    /// заводили и удалили, уже не «первый запуск», и повтор имени удалённого счёта
    /// был бы неотличим от ошибки сопоставления.
    /// </summary>
    private static async Task<bool> IsEmptyAsync(FinanceDbContext context, CancellationToken cancellationToken) =>
        !await context.Accounts.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false)
        && !await context.Places.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false)
        && !await context.Transactions.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Заводит счета в порядке файла. Уникальность имени — то же правило, что у формы
    /// счёта: операции файла ссылаются на счёт именем, и два одноимённых счёта
    /// сделали бы ссылку неоднозначной.
    /// </summary>
    private static Dictionary<string, Account> AddAccounts(
        FinanceDbContext context,
        IReadOnlyList<WalletImportAccount> source,
        DateOnly today,
        DateTimeOffset now)
    {
        Dictionary<string, Account> accounts = new(StringComparer.Ordinal);

        for (int order = 0; order < source.Count; order++)
        {
            WalletImportAccount item = source[order];
            NameUniqueness.Ensure(item.Name, accounts.Values.Select(static account => account.Name), RuleText.SubjectAccount);

            Account account = Account.Create(
                item.Name, item.Type, item.Currency, item.OpeningBalance, item.OpenedOn,
                item.ExcludedFromTotals, order, today, now);

            context.Accounts.Add(account.ToRow());
            accounts.Add(item.Name, account);
        }

        return accounts;
    }

    /// <summary>
    /// Заводит места файла. Одинаковые имена отвергаются тем же правилом, что
    /// у справочника мест: без учёта регистра и пробелов по краям.
    /// </summary>
    private static Dictionary<string, Guid> AddPlaces(
        FinanceDbContext context,
        IReadOnlyList<string> source,
        DateTimeOffset now)
    {
        Dictionary<string, Guid> places = new(StringComparer.Ordinal);
        List<string> taken = [];

        foreach (string name in source)
        {
            NameUniqueness.Ensure(name, taken, RuleText.SubjectPlace);

            Place place = Place.Create(name, now);
            context.Places.Add(place.ToRow());
            places.Add(name, place.Key);
            taken.Add(place.Name);
        }

        return places;
    }

    /// <summary>
    /// Живые категории базы — по текстовому ключу стартового набора и по идентификатору.
    /// Ключ, которого в наборе нет или чья категория удалена, в первый словарь
    /// не попадёт, и операция с ним остановит перенос. Второй нужен ради групп:
    /// вид хранится на группе, и проверка вида без неё невозможна.
    /// </summary>
    private static async Task<(Dictionary<string, Category> ByTextKey, Dictionary<Guid, Category> ByKey)> ReadCategoriesAsync(
        FinanceDbContext context,
        CancellationToken cancellationToken)
    {
        List<CategoryRow> rows = await context.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Category> byKey = rows
            .Select(static row => row.ToDomain())
            .ToDictionary(static category => category.Key);

        Dictionary<string, Category> byTextKey = new(StringComparer.Ordinal);

        foreach (PresetCategory preset in Preset.Embedded().All())
        {
            if (byKey.TryGetValue(preset.Id, out Category? category))
            {
                byTextKey.Add(preset.Key, category);
            }
        }

        return (byTextKey, byKey);
    }

    private static Transaction BuildTransaction(
        WalletImportTransaction item,
        int index,
        Dictionary<string, Account> accounts,
        Dictionary<string, Guid> places,
        Dictionary<string, Category> categoriesByTextKey,
        Dictionary<Guid, Category> categoriesByKey,
        DateOnly today)
    {
        Account source = accounts.GetValueOrDefault(item.SourceAccount)
            ?? throw new InvalidOperationException(ImportFaults.WalletImportUnknownAccount(index, item.SourceAccount));

        Account? target = item.TargetAccount is { } targetName
            ? accounts.GetValueOrDefault(targetName)
              ?? throw new InvalidOperationException(ImportFaults.WalletImportUnknownAccount(index, targetName))
            : null;

        Guid? placeKey = item.Place is { } placeName
            ? places.TryGetValue(placeName, out Guid found)
                ? found
                : throw new InvalidOperationException(ImportFaults.WalletImportUnknownPlace(index, placeName))
            : null;

        Category? category = item.Category is { } categoryKey
            ? categoriesByTextKey.GetValueOrDefault(categoryKey)
              ?? throw new InvalidOperationException(ImportFaults.WalletImportUnknownCategory(index, categoryKey))
            : null;

        Category? group = category?.ParentKey is { } parentKey
            ? categoriesByKey.GetValueOrDefault(parentKey)
            : null;

        // Сумма у перевода в файле одна: при разных валютах её пришлось бы
        // молча пересчитать один к одному
        if (target is not null && target.Currency != source.Currency)
        {
            throw new InvalidOperationException(ImportFaults.WalletImportTransferCurrencies(index));
        }

        Money amount = Money.Create(item.Amount, source.Currency);
        Money? targetAmount = target is null ? null : Money.Create(item.Amount, target.Currency);

        // Момент записи — когда запись завели в Wallet, а не когда её перенесли:
        // по нему лента упорядочивает операции одного дня
        Transaction transaction = Transaction.Create(
            item.Kind, source.Key, amount, target?.Key, targetAmount,
            category?.Key, placeKey, item.OccurredOn, item.Note, today, item.CreatedAtUtc);

        TransactionRules.EnsureValid(transaction, source, target, category, group);

        return transaction;
    }
}
