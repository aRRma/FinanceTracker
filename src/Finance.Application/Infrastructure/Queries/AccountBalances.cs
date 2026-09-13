using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Баланс счёта: начальный остаток плюс операции. Живёт в инфраструктуре, а не
/// в слайсе, потому что нужен двоим — главному экрану и справочнику счетов.
/// </summary>
/// <remarks>
/// Считается тремя сводными запросами на стороне базы, а не перебором операций
/// в памяти: при пятидесяти тысячах операций перебор не уложится в 300 мс.
/// Счёт списания и счёт зачисления спрашиваются раздельно — условие
/// <c>source = ? OR target = ?</c> не берёт ни один из двух индексов.
/// </remarks>
internal static class AccountBalances
{
    /// <summary>
    /// Читает движение по всем счетам одним проходом: сколько ушло и пришло
    /// по каждому. Начальные остатки прибавляет вызывающий — они лежат в счёте.
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static async Task<Dictionary<Guid, decimal>> ReadMovementsAsync(
        FinanceDbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outgoing = await context.Transactions
            .AsNoTracking()
            .GroupBy(row => new { row.SourceAccountKey, row.Kind })
            .Select(group => new
            {
                group.Key.SourceAccountKey,
                group.Key.Kind,
                Total = group.Sum(row => row.Amount)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var incoming = await context.Transactions
            .AsNoTracking()
            .Where(row => row.TargetAccountKey != null)
            .GroupBy(row => row.TargetAccountKey!.Value)
            .Select(group => new
            {
                AccountKey = group.Key,
                Total = group.Sum(row => row.TargetAmount!.Value)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> movements = [];

        foreach (var side in outgoing)
        {
            // Знак задаёт вид операции, а не знак суммы: сумма всегда положительна.
            // Перевод уменьшает счёт списания наравне с расходом — деньги с него ушли
            decimal delta = side.Kind is TransactionKind.Income ? side.Total : -side.Total;

            movements[side.SourceAccountKey] = movements.GetValueOrDefault(side.SourceAccountKey) + delta;
        }

        foreach (var side in incoming)
        {
            movements[side.AccountKey] = movements.GetValueOrDefault(side.AccountKey) + side.Total;
        }

        return movements;
    }
}
