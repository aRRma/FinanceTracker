using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Счёт по умолчанию — тот, что подставляется в новую операцию. Хранится только явный выбор пользователя,
/// а сам счёт вычисляется при чтении: поддерживать его записью пришлось бы в каждом обработчике, меняющем счета.
/// </summary>
public static class DefaultAccount
{
    /// <summary>
    /// Счёт по умолчанию: выбранный, пока он не заблокирован и не удалён, иначе верхний незаблокированный.
    /// Так первый заведённый счёт сам становится счётом по умолчанию, а заблокированный уступает место следующему.
    /// </summary>
    /// <param name="open">Незаблокированные счета в порядке экрана «Счета» (<see cref="InScreenOrder"/>).</param>
    /// <param name="choice">Явный выбор пользователя; пусто — выбора не было.</param>
    /// <returns>Счёт по умолчанию; пусто — незаблокированных счетов нет.</returns>
    public static OpenAccount? Resolve(IReadOnlyList<OpenAccount> open, Guid? choice)
    {
        ArgumentNullException.ThrowIfNull(open);

        foreach (OpenAccount account in open)
        {
            if (account.Key == choice)
            {
                return account;
            }
        }

        return open.Count > 0 ? open[0] : null;
    }

    /// <summary>
    /// Незаблокированные счета в порядке экрана «Счета»: доступные к тратам раньше накоплений,
    /// внутри раздела — порядок списка. По общему порядку первым оказался бы скрытый счёт,
    /// заведённый раньше остальных, хотя на экране он ниже.
    /// </summary>
    /// <param name="accounts">Счета в порядке <see cref="IAccountsQuery"/>.</param>
    /// <returns>Кандидаты в счета по умолчанию, верхний — первым.</returns>
    public static IEnumerable<AccountListItem> InScreenOrder(IEnumerable<AccountListItem> accounts) =>
        accounts
            .Where(static account => !account.IsClosed)
            .OrderBy(static account => account.ExcludedFromTotals);

    /// <summary>
    /// Кандидаты в счета по умолчанию для <see cref="Resolve"/> — в порядке <see cref="InScreenOrder"/>.
    /// Один путь на всех потребителей: свой список без этого порядка назвал бы не тот счёт.
    /// </summary>
    /// <param name="accounts">Счета в порядке <see cref="IAccountsQuery"/>.</param>
    /// <returns>Незаблокированные счета, верхний — первым.</returns>
    public static List<OpenAccount> Candidates(IEnumerable<AccountListItem> accounts) =>
        [.. InScreenOrder(accounts).Select(static account => new OpenAccount(account.Key, account.Name))];

    /// <summary>
    /// Разбирает значение настройки. Испорченное значение равно отсутствию выбора:
    /// подставится верхний счёт, а не ошибка на каждом открытии формы.
    /// </summary>
    /// <param name="value">Значение строки настройки.</param>
    /// <returns>Ключ выбранного счёта или <c>null</c>.</returns>
    public static Guid? Parse(string? value) => Guid.TryParse(value, out Guid key) ? key : null;

    // Порядок — тот же, что у InScreenOrder поверх AccountsQuery: иначе подтверждение
    // и раздел «Ещё» назвали бы не тот счёт, что подставит форма
    private static async Task<IReadOnlyList<OpenAccount>> ReadOpenAsync(
        FinanceDbContext context,
        CancellationToken cancellationToken) =>
        await context.Accounts
            .AsNoTracking()
            .Where(static row => !row.IsClosed)
            .OrderBy(static row => row.ExcludedFromTotals)
            .ThenBy(static row => row.SortOrder)
            .ThenBy(static row => row.Name)
            .Select(static row => new OpenAccount(row.Key, row.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static async Task<Guid?> ReadChoiceAsync(FinanceDbContext context, CancellationToken cancellationToken)
    {
        string? value = await context.Settings
            .AsNoTracking()
            .Where(static row => row.Name == SettingName.DefaultAccountKey)
            .Select(static row => row.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Parse(value);
    }

    /// <summary>
    /// Читает незаблокированные счета и выбор в контексте вызывающего и называет счёт по умолчанию:
    /// чтение и правило «кто следующий» не расходятся у карточки счёта и раздела «Ещё».
    /// </summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Незаблокированные счета и счёт по умолчанию среди них; пусто — счетов нет.</returns>
    public static async Task<(IReadOnlyList<OpenAccount> Open, OpenAccount? Current)> ResolveAsync(
        FinanceDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<OpenAccount> open = await ReadOpenAsync(context, cancellationToken).ConfigureAwait(false);
        Guid? choice = await ReadChoiceAsync(context, cancellationToken).ConfigureAwait(false);

        return (open, Resolve(open, choice));
    }

    /// <summary>
    /// Забывает выбор, если он указывает на уходящий — заблокированный или удалённый — счёт.
    /// Без этого разблокированный счёт молча вернул бы себе роль, которую уже занял другой.
    /// Без явного выбора роль следует за верхним в списке — и после разблокировки тоже, это решено намеренно.
    /// Зовётся в транзакции самой правки счёта: отдельная запись после неё могла бы не состояться.
    /// </summary>
    /// <param name="context">Контекст транзакции правки счёта.</param>
    /// <param name="leaving">Ключ уходящего счёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public static async Task ForgetAsync(FinanceDbContext context, Guid leaving, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        SettingRow? row = await context.Settings
            .FirstOrDefaultAsync(static existing => existing.Name == SettingName.DefaultAccountKey, cancellationToken)
            .ConfigureAwait(false);

        // Физическое удаление, как у любой настройки: обмену она не подлежит
        if (row is not null && Parse(row.Value) == leaving)
        {
            context.Settings.Remove(row);
        }
    }
}
