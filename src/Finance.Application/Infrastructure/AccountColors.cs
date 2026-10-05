using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Очередь, в которой цвет достаётся новому счёту сам. Первые три цвета различимы
/// и при дальтонизме, а близкие пары — красный с оранжевым и розовым, розовый
/// с пурпурным — появляются только с седьмого-восьмого счёта.
/// </summary>
public static class AccountColors
{
    private static readonly AccountColor[] Order =
    [
        AccountColor.Blue, AccountColor.Orange, AccountColor.Sky, AccountColor.Green,
        AccountColor.Violet, AccountColor.Magenta, AccountColor.Red, AccountColor.Pink
    ];

    /// <summary>
    /// Цвета в порядке очереди. Так же они стоят на экране выбора цвета.
    /// </summary>
    public static IReadOnlyList<AccountColor> Queue => Order;

    /// <summary>
    /// Значок на заливке этого цвета тёмный, а не белый: на светлых оранжевом, голубом
    /// и зелёном белый не дотягивает до порога различимости знака.
    /// </summary>
    /// <param name="color">Цвет счёта.</param>
    public static bool TakesDarkGlyph(AccountColor color) =>
        color is AccountColor.Orange or AccountColor.Sky or AccountColor.Green;

    /// <summary>
    /// Цвет нового счёта: первый по очереди среди самых редких. Пока свободные есть —
    /// первый свободный; заняты все — снова по кругу, начиная с тех, что повторены
    /// меньше других.
    /// </summary>
    /// <param name="taken">Цвета счетов, которые занимают цвет, — незаблокированных.</param>
    public static AccountColor Next(IEnumerable<AccountColor> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        int[] uses = new int[Order.Length];

        foreach (AccountColor color in taken)
        {
            int index = Array.IndexOf(Order, color);

            if (index >= 0)
            {
                uses[index]++;
            }
        }

        // Первый из самых редких: строгое «меньше» оставляет ничью за тем, кто раньше в очереди
        int best = 0;

        for (int index = 1; index < uses.Length; index++)
        {
            if (uses[index] < uses[best])
            {
                best = index;
            }
        }

        return Order[best];
    }

    /// <summary>
    /// Цвет, который получит заведённый сейчас счёт. Заблокированные цвет не занимают:
    /// счёт выведен из употребления, а его цвет нужнее новому.
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    internal static async Task<AccountColor> NextFreeAsync(FinanceDbContext context, CancellationToken cancellationToken)
    {
        List<AccountColor> taken = await context.Accounts
            .AsNoTracking()
            .Where(static row => !row.IsClosed)
            .Select(static row => row.Color)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Next(taken);
    }
}
