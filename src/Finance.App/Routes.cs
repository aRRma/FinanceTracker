using Finance.App.Pages;

namespace Finance.App;

/// <summary>
/// Маршруты экранов, лежащих за вкладками. Имена собраны в одном месте и
/// разыменовываются типизированными переходами: строка маршрута, набранная руками
/// на странице, ошибается молча — переход просто ничего не делает.
///
/// Первый сегмент составного маршрута не совпадает ни с именем вкладки, ни
/// с другим зарегистрированным маршрутом: Shell разбирает переход посегментно
/// и кладёт в стек страницу каждого известного ему сегмента. Лента счёта
/// поэтому <c>account-feed</c>, а не <c>accounts/feed</c> — иначе «назад»
/// уводило бы в справочник счетов, которого пользователь не открывал.
/// </summary>
public static class Routes
{
    /// <summary>
    /// Справочник счетов.
    /// </summary>
    public const string Accounts = "accounts";

    /// <summary>
    /// Карточка счёта: заведение и правка.
    /// </summary>
    public const string Account = "account-card";

    /// <summary>
    /// Справочник мест.
    /// </summary>
    public const string Places = "places";

    /// <summary>
    /// Карточка места: переименование и удаление. Параметр <c>key</c> — ключ места.
    /// </summary>
    public const string Place = "place-card";

    /// <summary>
    /// Справочник категорий.
    /// </summary>
    public const string Categories = "categories";

    /// <summary>
    /// Карточка группы: заведение и правка. Параметр <c>key</c> — ключ группы.
    /// </summary>
    public const string Group = "category-group";

    /// <summary>
    /// Карточка подкатегории. Параметр <c>key</c> — ключ правимой подкатегории,
    /// <c>group</c> — группа, в которой заводится новая.
    /// </summary>
    public const string Subcategory = "category-subcategory";

    /// <summary>
    /// Лента одного счёта. Параметр <c>key</c> — ключ счёта.
    /// </summary>
    public const string AccountFeed = "account-feed";

    /// <summary>
    /// Форма операции: запись и правка. Параметр <c>key</c> — ключ правимой операции,
    /// <c>account</c> — счёт для подстановки в новую, <c>kind</c> — её вид
    /// (<c>Expense</c>, <c>Income</c>, <c>Transfer</c>): им приходят с ярлыка на значке.
    /// </summary>
    public const string Transaction = "transactions/transaction";

    /// <summary>
    /// Выбор счёта для формы операции. Параметры: <c>kind</c> — вид операции, <c>selected</c> —
    /// счёт, стоящий в форме, <c>excluded</c> — счёт, которого в списке быть не должно,
    /// <c>target</c> — выбирается счёт зачисления перевода.
    /// </summary>
    public const string PickAccount = "transactions/pick-account";

    /// <summary>
    /// Выбор подкатегории. Параметры: <c>kind</c> — вид операции, <c>selected</c> — выбранная сейчас.
    /// </summary>
    public const string PickCategory = "transactions/pick-category";

    /// <summary>
    /// Выбор места. Параметр <c>current</c> — место, стоящее в форме.
    /// </summary>
    public const string PickPlace = "transactions/pick-place";

    /// <summary>
    /// Второй уровень отчёта: подкатегории группы. Параметры <c>key</c> — ключ группы,
    /// <c>month</c> — месяц в виде «2026-08». Первый сегмент — не имя вкладки «report»
    /// намеренно, по той же причине, что и у ленты счёта.
    /// </summary>
    public const string ReportGroup = "reports/group";

    /// <summary>
    /// Третий уровень отчёта: операции подкатегории. Параметры те же, <c>key</c> — ключ подкатегории.
    /// </summary>
    public const string ReportSubcategory = "reports/subcategory";

    /// <summary>
    /// Выбор темы оформления.
    /// </summary>
    public const string Appearance = "settings/appearance";

    /// <summary>
    /// Выбор часового пояса.
    /// </summary>
    public const string TimeZone = "settings/time-zone";

    /// <summary>
    /// Версия приложения и номер схемы базы.
    /// </summary>
    public const string About = "settings/about";

    /// <summary>
    /// Регистрирует маршруты в каркасе навигации.
    /// </summary>
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
        Routing.RegisterRoute(PickAccount, typeof(AccountPickerPage));
        Routing.RegisterRoute(PickCategory, typeof(CategoryPickerPage));
        Routing.RegisterRoute(PickPlace, typeof(PlacePickerPage));
        Routing.RegisterRoute(ReportGroup, typeof(ReportGroupPage));
        Routing.RegisterRoute(ReportSubcategory, typeof(ReportSubcategoryPage));
        Routing.RegisterRoute(Appearance, typeof(AppearancePage));
        Routing.RegisterRoute(TimeZone, typeof(TimeZonePage));
        Routing.RegisterRoute(About, typeof(AboutPage));
    }
}
