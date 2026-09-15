using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Accounts.Catalog;

/// <summary>Строка справочника счетов: значок, название, подпись, баланс и признаки.</summary>
/// <param name="Key">Ключ счёта.</param>
/// <param name="Icon">Ключ значка: наличные, карта или накопления.</param>
/// <param name="Name">Наименование счёта.</param>
/// <param name="Caption">Подпись под названием: тип и валюта.</param>
/// <param name="Balance">Баланс, уже отформатированный.</param>
/// <param name="IsNegative">Баланс отрицателен.</param>
/// <param name="IsClosed">Счёт закрыт — показан в отдельном разделе и погашенным.</param>
public sealed record AccountRowItem(
    Guid Key,
    string Icon,
    string Name,
    string Caption,
    string Balance,
    bool IsNegative,
    bool IsClosed)
{
    /// <summary>Счёт действующий: его строку перетаскивают, и у неё есть ручка.</summary>
    public bool IsOpen => !IsClosed;

    /// <summary>Собирает строку справочника из строки списка счетов.</summary>
    /// <param name="account">Счёт с уже посчитанным балансом.</param>
    public static AccountRowItem From(AccountListItem account)
    {
        ArgumentNullException.ThrowIfNull(account);

        string kind = account.Type is AccountType.Cash ? "Наличные" : "Карта";

        return new AccountRowItem(
            account.Key,
            AccountIcon.For(account.Type, account.ExcludedFromTotals),
            account.Name,
            $"{kind} · {account.Balance.Currency}",
            account.Balance.Display,
            account.Balance.IsNegative,
            account.IsClosed);
    }
}
