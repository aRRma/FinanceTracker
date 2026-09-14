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

    /// <summary>Справочник мест.</summary>
    public const string Places = "places";

    /// <summary>Карточка места: переименование и удаление. Параметр <c>key</c> — ключ места.</summary>
    public const string Place = "places/place";

    /// <summary>Справочник категорий.</summary>
    public const string Categories = "categories";

    /// <summary>Карточка группы: заведение и правка. Параметр <c>key</c> — ключ группы.</summary>
    public const string Group = "categories/group";

    /// <summary>
    /// Карточка подкатегории. Параметр <c>key</c> — ключ правимой подкатегории,
    /// <c>group</c> — группа, в которой заводится новая.
    /// </summary>
    public const string Subcategory = "categories/subcategory";

    /// <summary>
    /// Лента одного счёта. Параметр <c>key</c> — ключ счёта.
    /// Первый сегмент не совпадает с маршрутом вкладки намеренно: Shell принял бы
    /// такой переход за переход на саму вкладку и бросил бы исключение.
    /// </summary>
    public const string AccountFeed = "accounts/feed";

    /// <summary>
    /// Форма операции: запись и правка. Параметр <c>key</c> — ключ правимой операции,
    /// <c>account</c> — счёт для подстановки в новую.
    /// </summary>
    public const string Transaction = "transactions/transaction";

    /// <summary>Выбор темы оформления.</summary>
    public const string Appearance = "settings/appearance";

    /// <summary>Выбор часового пояса.</summary>
    public const string TimeZone = "settings/time-zone";

    /// <summary>Версия приложения и номер схемы базы.</summary>
    public const string About = "settings/about";

    /// <summary>Регистрирует маршруты в каркасе навигации.</summary>
    public static void Register()
    {
        Routing.RegisterRoute(Accounts, typeof(AccountsPage));
        Routing.RegisterRoute(Account, typeof(AccountPage));
        Routing.RegisterRoute(Places, typeof(PlacesPage));
        Routing.RegisterRoute(Place, typeof(PlacePage));
        Routing.RegisterRoute(Categories, typeof(CategoriesPage));
        Routing.RegisterRoute(Group, typeof(GroupPage));
        Routing.RegisterRoute(Subcategory, typeof(SubcategoryPage));
        Routing.RegisterRoute(AccountFeed, typeof(AccountFeedPage));
        Routing.RegisterRoute(Transaction, typeof(TransactionPage));
        Routing.RegisterRoute(Appearance, typeof(AppearancePage));
        Routing.RegisterRoute(TimeZone, typeof(TimeZonePage));
        Routing.RegisterRoute(About, typeof(AboutPage));
    }
}
