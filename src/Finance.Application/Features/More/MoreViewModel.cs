using Finance.Application.Texts;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.AppLock;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Features.More;

/// <summary>
/// Раздел «Ещё»: вход в справочники и настройки. Подписи считают содержимое —
/// по ним видно, что справочник не пуст, ещё до захода в него.
/// </summary>
public sealed partial class MoreViewModel : ScreenViewModel
{
    private readonly IAccountsQuery _accounts;
    private readonly IPlacesQuery _places;
    private readonly ICategoriesQuery _categories;
    private readonly ISettingsSummaryQuery _settings;
    private readonly AppLockService _appLock;

    /// <summary>
    /// Создаёт модель представления раздела «Ещё».
    /// </summary>
    /// <param name="accounts">Список счетов.</param>
    /// <param name="places">Справочник мест.</param>
    /// <param name="categories">Список категорий.</param>
    /// <param name="settings">Состояние настроек: тема, пояс, счёт по умолчанию, версия, схема.</param>
    /// <param name="appLock">Защита входа: включена ли.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public MoreViewModel(
        IAccountsQuery accounts,
        IPlacesQuery places,
        ICategoriesQuery categories,
        ISettingsSummaryQuery settings,
        AppLockService appLock,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(appLock);

        _accounts = accounts;
        _places = places;
        _categories = categories;
        _settings = settings;
        _appLock = appLock;
    }

    /// <summary>
    /// Сколько заведено счетов.
    /// </summary>
    [ObservableProperty]
    public partial string AccountsCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Сколько накопилось мест.
    /// </summary>
    [ObservableProperty]
    public partial string PlacesCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Сколько заведено групп и подкатегорий.
    /// </summary>
    [ObservableProperty]
    public partial string CategoriesCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Выбранная тема оформления.
    /// </summary>
    [ObservableProperty]
    public partial string ThemeCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Действующий часовой пояс.
    /// </summary>
    [ObservableProperty]
    public partial string TimeZoneCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Счёт по умолчанию — имя того счёта, что подставит форма операции.
    /// </summary>
    [ObservableProperty]
    public partial string DefaultAccountCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Включена ли защита входа.
    /// </summary>
    [ObservableProperty]
    public partial string AppLockCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Версия приложения и номер схемы базы.
    /// </summary>
    [ObservableProperty]
    public partial string AboutCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Перечитывает подписи.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // Четыре чтения независимы и идут разом: у каждого запроса свой контекст,
        // а последовательно экран ждал бы сумму четырёх обращений к базе
        Task<IReadOnlyList<AccountListItem>> accountsTask = _accounts.ReadAsync(cancellationToken);
        Task<IReadOnlyList<PlaceListItem>> placesTask = _places.ReadAsync(cancellationToken);
        Task<IReadOnlyList<CategoryListItem>> categoriesTask = _categories.ReadAsync(cancellationToken);
        Task<SettingsSummary> settingsTask = _settings.ReadAsync(cancellationToken);

        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        await Task.WhenAll(accountsTask, placesTask, categoriesTask, settingsTask);

        IReadOnlyList<AccountListItem> accounts = await accountsTask;
        IReadOnlyList<PlaceListItem> places = await placesTask;
        IReadOnlyList<CategoryListItem> categories = await categoriesTask;
        SettingsSummary settings = await settingsTask;

        AccountsCaption = Plural.Of(accounts.Count, UiTexts.MoreAccountsOne, UiTexts.MoreAccountsFew, UiTexts.MoreAccountsMany);
        PlacesCaption = Plural.Of(places.Count, UiTexts.MorePlacesOne, UiTexts.MorePlacesFew, UiTexts.MorePlacesMany);

        int groups = categories.Count(static category => category.IsGroup);

        CategoriesCaption =
            string.Format(
                UiCulture.Current,
                UiTexts.MoreCategoriesCaption,
                Plural.Of(groups, UiTexts.MoreGroupsOne, UiTexts.MoreGroupsFew, UiTexts.MoreGroupsMany),
                Plural.Of(
                    categories.Count - groups,
                    UiTexts.MoreSubcategoriesOne,
                    UiTexts.MoreSubcategoriesFew,
                    UiTexts.MoreSubcategoriesMany));

        ThemeCaption = settings.Theme.Caption;

        // У системного пояса называется и то, откуда он взят: иначе непонятно,
        // почему после переезда подпись сменилась сама. Зона названа городом, как
        // в списке поясов; зона без названия — идентификатором
        string zone = CityNames.Of(settings.TimeZoneId);

        TimeZoneCaption = settings.TimeZoneFromSystem
            ? string.Format(UiCulture.Current, UiTexts.MoreTimeZoneFromSystem, zone)
            : zone;

        // Без незаблокированных счетов выбирать нечего — подпись говорит почему,
        // тем же текстом, что пустой экран выбора
        DefaultAccountCaption = settings.DefaultAccount?.Name ?? UiTexts.DefaultAccountEmptyTitle;

        // Защита входа живёт вне базы и оповещения об изменении не шлёт:
        // подпись читается при каждом появлении раздела, как и всё здесь
        AppLockCaption = _appLock.IsEnabled ? UiTexts.AppLockOn : UiTexts.AppLockOff;

        AboutCaption = string.Format(UiCulture.Current, UiTexts.MoreAbout, settings.Version, settings.Schema);
    }

    /// <inheritdoc />
    protected override DataChange Watched =>
        DataChange.Accounts | DataChange.Places | DataChange.Categories | DataChange.Settings;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
