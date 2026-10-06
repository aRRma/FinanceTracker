using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Счета, входящие в общие суммы: счета одной валюты без признака «скрытый»,
/// заблокированные тоже. Один предикат на итог дня общей ленты (в рублях) и на
/// «все активные» счета отчёта: разойдясь, они дали бы два разных ответа на один
/// вопрос, и разницу было бы не с чем сверить.
/// </summary>
internal static class CountedAccounts
{
    /// <summary>
    /// Выборка счетов валюты, попадающих в суммы.
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="currency">Валюта сумм: итог дня — всегда рубль, отчёт — валюта своего выбора.</param>
    public static IQueryable<AccountRow> Of(FinanceDbContext context, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Accounts
            .AsNoTracking()
            .Where(account => account.Currency == currency && !account.ExcludedFromTotals);
    }
}
