using System.Collections.Frozen;
using Finance.Application.Texts;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Название экрана по маршруту из следа действий: «О программе» вместо <c>//more/settings/about</c>.
/// </summary>
/// <remarks>
/// След хранит маршрут, а не заголовок страницы: в заголовке бывает имя счёта, а в отчёт имена не идут.
/// Ключи — маршруты вкладок и <c>Routes</c> приложения; что у каждого маршрута есть название, сверяет
/// тест прикладного слоя <c>Названия_экранов_совпадают_с_маршрутами_приложения</c>. Названия — заголовки самих экранов, у экранов с именем в заголовке — общее слово.
/// </remarks>
internal static class CrashScreens
{
    private static readonly FrozenDictionary<string, Func<string>> Names = new Dictionary<string, Func<string>>
    {
        ["balances"] = static () => UiTexts.BalancesTitle,
        ["feed"] = static () => UiTexts.FeedTitle,
        ["report"] = static () => UiTexts.ReportTitle,
        ["more"] = static () => UiTexts.MoreTitle,
        ["accounts"] = static () => UiTexts.AccountsTitle,
        ["account-card"] = static () => UiTexts.AccountTitleExisting,
        ["account-badge"] = static () => UiTexts.AccountBadgeTitle,
        ["places"] = static () => UiTexts.PlacesTitle,
        ["place-card"] = static () => UiTexts.PlaceTitle,
        ["categories"] = static () => UiTexts.CategoriesTitle,
        ["category-group"] = static () => UiTexts.GroupTitleExisting,
        ["category-subcategory"] = static () => UiTexts.SubcategoryTitleExisting,
        ["account-feed"] = static () => UiTexts.CrashScreenAccountFeed,
        ["transactions/transaction"] = static () => UiTexts.TransactionTitleExisting,
        ["transactions/pick-account"] = static () => UiTexts.PickAccountTitle,
        ["transactions/pick-category"] = static () => UiTexts.PickCategoryTitle,
        ["transactions/pick-place"] = static () => UiTexts.PickPlaceTitle,
        ["reports/group"] = static () => UiTexts.ReportTitle,
        ["reports/subcategory"] = static () => UiTexts.ReportTitle,
        ["reports/accounts"] = static () => UiTexts.ReportAccountsTitle,
        ["settings/appearance"] = static () => UiTexts.AppearanceTitle,
        ["settings/time-zone"] = static () => UiTexts.TimeZoneTitle,
        ["settings/default-account"] = static () => UiTexts.DefaultAccountTitle,
        ["settings/about"] = static () => UiTexts.AboutTitle,
        ["settings/crash-reports"] = static () => UiTexts.CrashReportsTitle,
        ["settings/crash-report"] = static () => UiTexts.CrashReportTitle,
        ["settings/data"] = static () => UiTexts.ExportTitle,
        ["settings/app-lock"] = static () => UiTexts.AppLockTitle,
        ["settings/pin"] = static () => UiTexts.AppLockTitle,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Длинные первыми: «reports/accounts» обязан победить «accounts»
    private static readonly string[] ByLength = [.. Names.Keys.OrderByDescending(static key => key.Length)];

    /// <summary>
    /// Маршруты, у которых есть название, — для сверки с маршрутами приложения.
    /// </summary>
    internal static IEnumerable<string> Routes => Names.Keys;

    /// <summary>
    /// Название экрана, на котором кончается адрес; пусто — маршрут незнакомый.
    /// </summary>
    /// <param name="location">Адрес из следа: <c>//more/settings/about</c>.</param>
    internal static string? Of(string location)
    {
        ArgumentNullException.ThrowIfNull(location);

        foreach (string route in ByLength)
        {
            if (location.EndsWith(route, StringComparison.Ordinal)
                && (location.Length == route.Length || location[^(route.Length + 1)] is '/'))
            {
                return Names[route]();
            }
        }

        return null;
    }
}
