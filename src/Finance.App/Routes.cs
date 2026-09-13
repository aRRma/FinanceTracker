using Finance.App.Pages;

namespace Finance.App;

/// <summary>
/// Маршруты экранов, лежащих за вкладками. Имена собраны в одном месте и
/// разыменовываются типизированными переходами: строка маршрута, набранная руками
/// на странице, ошибается молча — переход просто ничего не делает.
/// </summary>
public static class Routes
{
    /// <summary>Справочник счетов.</summary>
    public const string Accounts = "accounts";

    /// <summary>Карточка счёта: заведение и правка.</summary>
    public const string Account = "accounts/account";

    /// <summary>Лента одного счёта. Параметр <c>key</c> — ключ счёта.</summary>
    public const string AccountFeed = "feed/account";

    /// <summary>
    /// Форма операции: запись и правка. Параметр <c>key</c> — ключ правимой операции,
    /// <c>account</c> — счёт для подстановки в новую.
    /// </summary>
    public const string Transaction = "transactions/transaction";

    /// <summary>Регистрирует маршруты в каркасе навигации.</summary>
    public static void Register()
    {
        Routing.RegisterRoute(Accounts, typeof(AccountsPage));
        Routing.RegisterRoute(Account, typeof(AccountPage));
        Routing.RegisterRoute(AccountFeed, typeof(AccountFeedPage));
        Routing.RegisterRoute(Transaction, typeof(TransactionPage));
    }
}
