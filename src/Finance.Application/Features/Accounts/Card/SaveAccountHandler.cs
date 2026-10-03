using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Errors;
using Finance.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Заводит и правит счёт. Проверки, которым мало одной записи — занятое имя,
/// операции по счёту, — читаются здесь и подаются домену параметром: решение
/// остаётся за доменом, а в хранилище он не ходит.
/// </summary>
public sealed class SaveAccountHandler : ISaveAccountHandler
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: «сегодня» пользователя и момент записи.</param>
    public SaveAccountHandler(UnitOfWork unitOfWork, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<Guid> HandleAsync(SaveAccountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Отказ, а не молчаливый сброс признака: вызывающий, приславший его,
        // получил бы открытый счёт и не узнал бы об этом
        if (command is { Key: null, IsClosed: true })
        {
            throw new ArgumentException(Faults.NewAccountClosed(), nameof(command));
        }

        return _unitOfWork.ExecuteAsync<Guid>(
            async (context, token) =>
            {
                Guid key = command.Key is { } existing
                    ? await UpdateAsync(context, command, existing, token).ConfigureAwait(false)
                    : await CreateAsync(context, command, token).ConfigureAwait(false);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return (key, DataChange.Accounts);
            },
            cancellationToken);
    }

    private async Task<Guid> CreateAsync(
        FinanceDbContext context,
        SaveAccountCommand command,
        CancellationToken cancellationToken)
    {
        await EnsureNameFreeAsync(context, command.Name, self: null, cancellationToken).ConfigureAwait(false);

        // Новый счёт встаёт в конец списка: место в нём выбирает пользователь
        // перетаскиванием, а не порядок заведения
        int? lastOrder = await context.Accounts
            .Select(row => (int?)row.SortOrder)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        int nextOrder = lastOrder is { } last ? last + 1 : 0;

        Account account = Account.Create(
            command.Name,
            command.Type,
            command.Currency,
            command.OpeningBalance,
            command.OpenedOn,
            command.ExcludedFromTotals,
            nextOrder,
            _clock.Today,
            _clock.NowUtc);

        context.Accounts.Add(account.ToRow());

        return account.Key;
    }

    private async Task<Guid> UpdateAsync(
        FinanceDbContext context,
        SaveAccountCommand command,
        Guid key,
        CancellationToken cancellationToken)
    {
        AccountRow row = await context.Accounts
            .FirstOrDefaultAsync(existing => existing.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(Faults.AccountNotFound(key));

        Account account = row.ToDomain();

        // Проверка и переименование выполняются всегда, а не только при изменении
        // имени по существу: сравнение уникальности не различает регистр, и правка
        // «наличные» на «Наличные» иначе молча не сохранялась бы
        await EnsureNameFreeAsync(context, command.Name, key, cancellationToken).ConfigureAwait(false);
        account.Rename(command.Name);

        account.ChangeType(command.Type);

        if (command.Currency != account.Currency)
        {
            bool hasEverHadTransactions = await AccountTransactions
                .ExistedEverAsync(context, key, cancellationToken)
                .ConfigureAwait(false);

            account.ChangeCurrency(command.Currency, hasEverHadTransactions);
        }

        account.ChangeOpeningBalance(command.OpeningBalance);

        if (command.OpenedOn != account.OpenedOn)
        {
            DateOnly? earliest = await AccountTransactions
                .EarliestOnAsync(context, key, cancellationToken)
                .ConfigureAwait(false);

            account.ChangeOpenedOn(command.OpenedOn, earliest, _clock.Today);
        }

        account.SetExcludedFromTotals(command.ExcludedFromTotals);

        if (command.IsClosed)
        {
            account.Close();
        }
        else
        {
            account.Reopen();
        }

        account.CopyTo(row);

        return key;
    }

    /// <summary>
    /// Имя счёта уникально по всему списку неудалённых счетов.
    /// </summary>
    private static async Task EnsureNameFreeAsync(
        FinanceDbContext context,
        string name,
        Guid? self,
        CancellationToken cancellationToken)
    {
        var taken = await context.Accounts
            .AsNoTracking()
            .Select(row => new { row.Key, row.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (self is { } key)
        {
            NameUniqueness.EnsureForRename(
                name,
                taken.Select(static account => (account.Key, account.Name)),
                key,
                RuleText.SubjectAccount);
        }
        else
        {
            NameUniqueness.Ensure(name, taken.Select(static account => account.Name), RuleText.SubjectAccount);
        }
    }
}
