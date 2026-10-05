using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Balances;

/// <summary>
/// Строка счёта на главном экране: значок, название и баланс.
/// </summary>
/// <param name="Key">Ключ счёта — по нему открывается лента.</param>
/// <param name="Icon">Ключ значка: выбранный руками или по типу.</param>
/// <param name="Color">Цвет счёта — заливка его знака.</param>
/// <param name="Name">Наименование счёта.</param>
/// <param name="Balance">Баланс, уже отформатированный.</param>
/// <param name="IsNegative">Баланс отрицателен: его показывают смысловым цветом.</param>
/// <param name="IsSavings">Счёт скрыт: строка приглушена, в подытог не входит.</param>
public sealed record AccountTile(
    Guid Key,
    string Icon,
    AccountColor Color,
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
            account.Icon,
            account.Color,
            account.Name,
            account.Balance.Display,
            account.Balance.IsNegative,
            account.ExcludedFromTotals);
    }
}
