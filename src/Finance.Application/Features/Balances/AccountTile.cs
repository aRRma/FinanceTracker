using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Balances;

/// <summary>
/// Строка счёта на главном экране: значок, название и баланс.
/// </summary>
/// <param name="Key">Ключ счёта — по нему открывается лента.</param>
/// <param name="Icon">Ключ значка: наличные, карта или накопления.</param>
/// <param name="Name">Наименование счёта.</param>
/// <param name="Balance">Баланс, уже отформатированный.</param>
/// <param name="IsNegative">Баланс отрицателен: его показывают смысловым цветом.</param>
/// <param name="IsSavings">Счёт скрыт: строка приглушена, в подытог не входит.</param>
public sealed record AccountTile(
    Guid Key,
    string Icon,
    string Name,
    string Balance,
    bool IsNegative,
    bool IsSavings)
{
    /// <summary>
    /// Собирает строку экрана из строки списка счетов.
    /// </summary>
    /// <param name="account">Счёт с уже посчитанным балансом.</param>
    public static AccountTile From(AccountListItem account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AccountTile(
            account.Key,
            AccountIcon.For(account.Type, account.ExcludedFromTotals),
            account.Name,
            account.Balance.Display,
            account.Balance.IsNegative,
            account.ExcludedFromTotals);
    }
}
