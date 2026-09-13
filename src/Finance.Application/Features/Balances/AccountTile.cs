using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Balances;

/// <summary>Строка счёта на главном экране: название, подпись и баланс.</summary>
/// <param name="Key">Ключ счёта — по нему открывается лента.</param>
/// <param name="Name">Наименование счёта.</param>
/// <param name="Kind">Наличные или карта — подпись под названием.</param>
/// <param name="Balance">Баланс, уже отформатированный.</param>
/// <param name="IsNegative">Баланс отрицателен: его показывают смысловым цветом.</param>
public sealed record AccountTile(Guid Key, string Name, string Kind, string Balance, bool IsNegative)
{
    /// <summary>Собирает строку экрана из строки списка счетов.</summary>
    /// <param name="account">Счёт с уже посчитанным балансом.</param>
    public static AccountTile From(AccountListItem account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AccountTile(
            account.Key,
            account.Name,
            account.Type is AccountType.Cash ? "Наличные" : "Карта",
            account.Balance.Display,
            account.Balance.IsNegative);
    }
}
