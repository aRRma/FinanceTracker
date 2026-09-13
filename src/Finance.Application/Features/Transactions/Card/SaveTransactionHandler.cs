using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Записывает и правит операцию. Счета и категорию читает сам и подаёт домену
/// параметром: решение о закрытом счёте, дате раньше открытия и виде категории
/// остаётся за <see cref="TransactionRules"/>, а в хранилище домен не ходит.
/// </summary>
public sealed class SaveTransactionHandler : ISaveTransactionHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Создаёт обработчик.</summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: «сегодня» пользователя и момент записи.</param>
    public SaveTransactionHandler(UnitOfWork unitOfWork, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<Guid> HandleAsync(SaveTransactionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _unitOfWork.ExecuteAsync<Guid>(
            async (context, token) =>
            {
                Account source = await TransactionSides
                    .AccountAsync(context, command.SourceAccountKey, token)
                    .ConfigureAwait(false);

                Account? target = command.TargetAccountKey is { } targetKey
                    ? await TransactionSides.AccountAsync(context, targetKey, token).ConfigureAwait(false)
                    : null;

                (Category? category, Category? group) = await CategoryAsync(context, command.CategoryKey, token)
                    .ConfigureAwait(false);

                (Guid? placeKey, bool placeCreated) = await ResolvePlaceAsync(context, command.PlaceName, token)
                    .ConfigureAwait(false);

                Money amount = Money.Create(command.Amount, source.Currency);
                Money? targetAmount = target is null ? null : TargetAmount(command, source, target);

                Guid key = command.Key is { } existing
                    ? await UpdateAsync(context, command, existing, source, target, category, group, amount, targetAmount, placeKey, token).ConfigureAwait(false)
                    : Create(context, command, source, target, category, group, amount, targetAmount, placeKey);

                await RememberAccountAsync(context, source.Key, token).ConfigureAwait(false);
                await context.SaveChangesAsync(token).ConfigureAwait(false);

                DataChange change = DataChange.Transactions;

                if (placeCreated)
                {
                    change |= DataChange.Places;
                }

                return (key, change);
            },
            cancellationToken);
    }

    private Guid Create(
        FinanceDbContext context,
        SaveTransactionCommand command,
        Account source,
        Account? target,
        Category? category,
        Category? group,
        Money amount,
        Money? targetAmount,
        Guid? placeKey)
    {
        Transaction transaction = Transaction.Create(
            command.Kind,
            source.Key,
            amount,
            target?.Key,
            targetAmount,
            command.CategoryKey,
            placeKey,
            command.OccurredOn,
            command.Note,
            _clock.Today,
            _clock.NowUtc);

        TransactionRules.EnsureValid(transaction, source, target, category, group);

        context.Transactions.Add(transaction.ToRow());

        return transaction.Key;
    }

    private async Task<Guid> UpdateAsync(
        FinanceDbContext context,
        SaveTransactionCommand command,
        Guid key,
        Account source,
        Account? target,
        Category? category,
        Category? group,
        Money amount,
        Money? targetAmount,
        Guid? placeKey,
        CancellationToken cancellationToken)
    {
        TransactionRow row = await context.Transactions
            .FirstOrDefaultAsync(existing => existing.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Операция {key} не найдена");

        // Два экземпляра из одной строки: один правится, другой остаётся снимком
        // «как было» — по нему домен отличает уже задействованный закрытый счёт
        // от подставленного впервые
        Transaction previous = await TransactionSides.ToDomainAsync(context, row, cancellationToken).ConfigureAwait(false);
        Transaction transaction = await TransactionSides.ToDomainAsync(context, row, cancellationToken).ConfigureAwait(false);

        transaction.Replace(
            command.Kind,
            source.Key,
            amount,
            target?.Key,
            targetAmount,
            command.CategoryKey,
            placeKey,
            command.OccurredOn,
            command.Note,
            _clock.Today);

        TransactionRules.EnsureValid(transaction, source, target, category, group, previous);

        transaction.CopyTo(row);

        return key;
    }

    /// <summary>
    /// Сумма зачисления. При одной валюте форма её не спрашивает — она равна сумме
    /// списания. При разных валютах без неё перевод не собрать, и это ошибка формы,
    /// а не ввода: форма обязана была спросить.
    /// </summary>
    private static Money TargetAmount(SaveTransactionCommand command, Account source, Account target)
    {
        if (command.TargetAmount is { } given)
        {
            return Money.Create(given, target.Currency);
        }

        if (source.Currency == target.Currency)
        {
            return Money.Create(command.Amount, target.Currency);
        }

        throw new ArgumentException(
            $"Перевод из {source.Currency} в {target.Currency} без суммы зачисления",
            nameof(command));
    }

    /// <summary>Подкатегория вместе с группой: вид хранится на группе, и проверка без неё невозможна.</summary>
    private static async Task<(Category? Category, Category? Group)> CategoryAsync(
        FinanceDbContext context,
        Guid? categoryKey,
        CancellationToken cancellationToken)
    {
        if (categoryKey is not { } key)
        {
            return (null, null);
        }

        CategoryRow row = await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Категория {key} не найдена");

        Category category = row.ToDomain();

        // Группа у подкатегории есть всегда; выбранная группа вместо подкатегории —
        // ввод пользователя, и это скажет домен, а не падение здесь
        if (category.ParentKey is not { } parentKey)
        {
            return (category, null);
        }

        CategoryRow parent = await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(group => group.Key == parentKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Группа {parentKey} подкатегории {key} не найдена");

        return (category, parent.ToDomain());
    }

    /// <summary>
    /// Место по названию: существующее — без учёта регистра и пробелов по краям,
    /// иначе новое. Заводится прямо из формы, чтобы не гонять пользователя в справочник
    /// ради одной строки.
    /// </summary>
    private async Task<(Guid? Key, bool Created)> ResolvePlaceAsync(
        FinanceDbContext context,
        string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, false);
        }

        var places = await context.Places
            .AsNoTracking()
            .Select(row => new { row.Key, row.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var place in places)
        {
            if (Names.AreSame(place.Name, name))
            {
                return (place.Key, false);
            }
        }

        Place created = Place.Create(name, _clock.NowUtc);
        context.Places.Add(created.ToRow());

        return (created.Key, true);
    }

    /// <summary>
    /// Запоминает счёт списания для следующей операции — в той же транзакции,
    /// а не отдельной записью после: иначе неудавшееся сохранение оставило бы
    /// в подстановке счёт операции, которой нет.
    /// </summary>
    private static async Task RememberAccountAsync(
        FinanceDbContext context,
        Guid accountKey,
        CancellationToken cancellationToken)
    {
        string value = accountKey.ToString();

        SettingRow? row = await context.Settings
            .FirstOrDefaultAsync(setting => setting.Name == SettingName.LastAccountKey, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            context.Settings.Add(new SettingRow { Name = SettingName.LastAccountKey, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }
}
