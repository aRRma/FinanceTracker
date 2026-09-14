using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Счета, входящие в общие суммы: рублёвые и без признака «скрыть из расчётов».
/// Один предикат на итог дня общей ленты и на отчёт: разойдясь, они дали бы два
/// разных ответа на один вопрос, и разницу было бы не с чем сверить.
/// </summary>
internal static class CountedAccounts
{
    /// <summary>Выборка счетов, попадающих в суммы.</summary>
    /// <param name="context">Контекст базы.</param>
    public static IQueryable<AccountRow> Of(FinanceDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Accounts
            .AsNoTracking()
            .Where(account => account.Currency == Currency.RUB && !account.ExcludedFromTotals);
    }
}
