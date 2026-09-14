using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
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

    /// <summary>Создаёт модель представления раздела «Ещё».</summary>
    /// <param name="accounts">Список счетов.</param>
    /// <param name="places">Справочник мест.</param>
    /// <param name="categories">Список категорий.</param>
    /// <param name="settings">Состояние настроек: тема, пояс, версия, схема.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public MoreViewModel(
        IAccountsQuery accounts,
        IPlacesQuery places,
        ICategoriesQuery categories,
        ISettingsSummaryQuery settings,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(settings);

        _accounts = accounts;
        _places = places;
        _categories = categories;
        _settings = settings;
    }

    /// <summary>Сколько заведено счетов.</summary>
    [ObservableProperty]
    public partial string AccountsCaption { get; private set; } = string.Empty;

    /// <summary>Сколько накопилось мест.</summary>
    [ObservableProperty]
    public partial string PlacesCaption { get; private set; } = string.Empty;

    /// <summary>Сколько заведено групп и подкатегорий.</summary>
    [ObservableProperty]
    public partial string CategoriesCaption { get; private set; } = string.Empty;

    /// <summary>Выбранная тема оформления.</summary>
    [ObservableProperty]
    public partial string ThemeCaption { get; private set; } = string.Empty;

    /// <summary>Действующий часовой пояс.</summary>
    [ObservableProperty]
    public partial string TimeZoneCaption { get; private set; } = string.Empty;

    /// <summary>Версия приложения и номер схемы базы.</summary>
    [ObservableProperty]
    public partial string AboutCaption { get; private set; } = string.Empty;

    /// <summary>Перечитывает подписи.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        IReadOnlyList<AccountListItem> accounts = await _accounts.ReadAsync(cancellationToken);
        IReadOnlyList<PlaceListItem> places = await _places.ReadAsync(cancellationToken);
        IReadOnlyList<CategoryListItem> categories = await _categories.ReadAsync(cancellationToken);

        AccountsCaption = Plural.Of(accounts.Count, "счёт", "счёта", "счетов");
        PlacesCaption = Plural.Of(places.Count, "место", "места", "мест");

        int groups = categories.Count(static category => category.IsGroup);

        CategoriesCaption =
            $"{Plural.Of(groups, "группа", "группы", "групп")}, "
            + Plural.Of(categories.Count - groups, "подкатегория", "подкатегории", "подкатегорий");

        SettingsSummary settings = await _settings.ReadAsync(cancellationToken);

        ThemeCaption = settings.Theme.Caption;

        // У системного пояса называется и то, откуда он взят: иначе непонятно,
        // почему после переезда подпись сменилась сама
        TimeZoneCaption = settings.TimeZoneFromSystem
            ? $"Как в системе · {settings.TimeZoneId}"
            : settings.TimeZoneId;

        AboutCaption = $"Версия {settings.Version}, схема {settings.Schema}";
    }

    /// <inheritdoc />
    protected override DataChange Watched =>
        DataChange.Accounts | DataChange.Places | DataChange.Categories | DataChange.Settings;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}
