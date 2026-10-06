using System.Collections.Frozen;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Report;

/// <summary>
/// Счета отчёта: по каким счетам он посчитан. Либо все активные счета одной валюты —
/// правилом, а не списком, — либо выбранные руками счета одной валюты. Валюта у набора
/// одна всегда: общей суммы по валютам не существует.
/// </summary>
/// <remarks>
/// «Все активные» хранятся правилом намеренно: новый счёт и снятый признак «скрытый»
/// попадают в отчёт сами, а снимок списка их бы не увидел.
/// </remarks>
public sealed class ReportAccounts
{
    private ReportAccounts(Currency currency, FrozenSet<Guid>? keys)
    {
        Currency = currency;
        Keys = keys;
    }

    /// <summary>
    /// Умолчание — все активные рублёвые счета: отчёт до появления выбора.
    /// </summary>
    public static ReportAccounts Default { get; } = new(Currency.RUB, keys: null);

    /// <summary>
    /// Валюта отчёта — в ней все его суммы.
    /// </summary>
    public Currency Currency { get; }

    /// <summary>
    /// Выбранные руками счета; <see langword="null"/> — все активные счета валюты.
    /// </summary>
    public FrozenSet<Guid>? Keys { get; }

    /// <summary>
    /// Все активные счета валюты, а не выбранные руками.
    /// </summary>
    public bool IsAllActive => Keys is null;

    /// <summary>
    /// Отчёт посчитан как обычно — по всем активным рублёвым счетам.
    /// </summary>
    public bool IsDefault => IsAllActive && Currency is Currency.RUB;

    /// <summary>
    /// Все активные счета валюты; у рубля это умолчание.
    /// </summary>
    /// <param name="currency">Валюта отчёта.</param>
    public static ReportAccounts AllActive(Currency currency)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(currency, Currency.Unknown);

        return currency is Currency.RUB ? Default : new ReportAccounts(currency, keys: null);
    }

    /// <summary>
    /// Выбранные руками счета одной валюты.
    /// </summary>
    /// <param name="currency">Валюта выбранных счетов.</param>
    /// <param name="keys">Ключи счетов, хотя бы один.</param>
    public static ReportAccounts Chosen(Currency currency, IEnumerable<Guid> keys)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(currency, Currency.Unknown);
        ArgumentNullException.ThrowIfNull(keys);

        FrozenSet<Guid> set = keys.ToFrozenSet();

        // Пустой набор — отчёт ни о чём: такой выбор становится умолчанием раньше,
        // чем доходит сюда
        ArgumentOutOfRangeException.ThrowIfZero(set.Count);

        return new ReportAccounts(currency, set);
    }

    /// <summary>
    /// Входит ли счёт в отчёт.
    /// </summary>
    /// <param name="account">Счёт из списка счетов отчёта.</param>
    public bool Includes(ReportAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return account.Currency == Currency && (Keys?.Contains(account.Key) ?? account.IsActive);
    }
}
