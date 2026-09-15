using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Чтение счетов операции на пишущем пути. Общее для записи и удаления: собрать
/// доменную операцию из строки без валют её счетов нельзя, а валюта у строки
/// не хранится — она у счёта каждой стороны.
/// </summary>
internal static class TransactionSides
{
    /// <summary>Читает счёт по ключу. Отсутствие — ошибка вызывающего, а не ввод пользователя.</summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="key">Ключ счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static async Task<Account> AccountAsync(
        FinanceDbContext context,
        Guid key,
        CancellationToken cancellationToken)
    {
        AccountRow row = await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Счёт {key} не найден");

        return row.ToDomain();
    }

    /// <summary>
    /// Валюты счетов операции — то, чего не хватает строке, чтобы стать доменной
    /// операцией. Читаются один раз на строку: правке нужны два экземпляра из одной
    /// строки, и читать счета под каждый значило бы ходить в базу дважды.
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="row">Строка операции.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static async Task<(Currency Source, Currency? Target)> CurrenciesAsync(
        FinanceDbContext context,
        TransactionRow row,
        CancellationToken cancellationToken)
    {
        Account source = await AccountAsync(context, row.SourceAccountKey, cancellationToken).ConfigureAwait(false);

        Currency? targetCurrency = row.TargetAccountKey is { } target
            ? (await AccountAsync(context, target, cancellationToken).ConfigureAwait(false)).Currency
            : null;

        return (source.Currency, targetCurrency);
    }

    /// <summary>Собирает доменную операцию из строки, дочитав валюты её счетов.</summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="row">Строка операции.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static async Task<Transaction> ToDomainAsync(
        FinanceDbContext context,
        TransactionRow row,
        CancellationToken cancellationToken)
    {
        (Currency source, Currency? target) = await CurrenciesAsync(context, row, cancellationToken).ConfigureAwait(false);

        return row.ToDomain(source, target);
    }
}
