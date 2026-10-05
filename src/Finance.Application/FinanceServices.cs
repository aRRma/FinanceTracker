using System.Globalization;
using Finance.Application.Features.Accounts.Badge;
using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Accounts.Catalog;
using Finance.Application.Features.Balances;
using Finance.Application.Features.Categories.Card;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Features.Export;
using Finance.Application.Features.Places.Card;
using Finance.Application.Features.Places.Catalog;
using Finance.Application.Features.Feed;
using Finance.Application.Features.More;
using Finance.Application.Features.Report;
using Finance.Application.Features.Settings.About;
using Finance.Application.Features.Settings.Appearance;
using Finance.Application.Features.Settings.TimeZones;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Deletion;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Finance.Application;

/// <summary>
/// Сборка прикладного слоя. Приложение MAUI подключает его одним вызовом и про
/// EF Core, SQLite и пути к файлам не знает: иначе слой доступа к данным протёк бы
/// в страницы, и тесты потребовали бы эмулятор.
/// </summary>
public static class FinanceServices
{
    /// <summary>
    /// Регистрирует хранилище, часы, справочник значков и подготовку базы.
    /// </summary>
    /// <param name="services">Набор служб приложения.</param>
    /// <param name="databasePath">Полный путь к файлу базы в папке данных приложения.</param>
    /// <param name="dispatchToInterface">Как выполнить действие в потоке интерфейса; пусто — оповещение приходит на месте, так работают тесты.</param>
    /// <param name="applyTheme">Как платформа переключает оформление; пусто — применять нечем.</param>
    /// <param name="applicationVersion">Версия приложения из манифеста для экрана «О программе».</param>
    /// <param name="culture">Язык текстов, счётных форм и дат; пусто — русский, пока единственный язык приложения.</param>
    /// <param name="cacheFolder">Папка временных файлов: выгрузка, пока её отдают, и присланный файл, пока его проверяют; пусто — папка рядом с базой.</param>
    /// <remarks>
    /// Поток интерфейса, оформление и версию знает только платформа: команды выполняются в фоне, а экраны
    /// по их итогу правят привязанные коллекции, и вернуть это в свой поток обязана она; что такое тёмное
    /// оформление окна, знает MAUI, а прикладной слой — только выбранную тему.
    /// </remarks>
    public static IServiceCollection AddFinance(
        this IServiceCollection services,
        string databasePath,
        Action<Action>? dispatchToInterface = null,
        Action<Theme>? applyTheme = null,
        string? applicationVersion = null,
        CultureInfo? culture = null,
        string? cacheFolder = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // До первого экрана: тексты и форматы берутся из статических точек,
        // и к моменту, когда модель представления соберёт первую подпись,
        // язык обязан быть уже выбран
        UiCulture.Use(culture);

        DatabaseLocation location = new(databasePath, cacheFolder);

        services.AddSingleton(location);
        services.AddSingleton(IconCatalog.Embedded());
        services.AddSingleton(new AboutInfo(applicationVersion));
        services.AddSingleton(new ThemeApplier(applyTheme, dispatchToInterface));

        // Часы регистрируются одним объектом под двумя именами: настройки меняют
        // зону через SystemClock, а читают время все остальные через IClock.
        // Источник момента — TryAdd: тесты подставляют свой, и «сегодня» у них
        // не зависит от дня и часа запуска
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SystemClock>();
        services.AddSingleton<IClock>(provider => provider.GetRequiredService<SystemClock>());

        services.AddDbContextFactory<FinanceDbContext>(options => options.UseSqlite(location.ConnectionString));

        services.AddSingleton<IChangeNotifier>(new ChangeNotifier(dispatchToInterface));
        services.AddSingleton<TimeZoneFollower>();
        services.AddSingleton<UnitOfWork>();
        services.AddSingleton<ILocalSettings, LocalSettings>();
        services.AddSingleton<DatabaseBootstrapper>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<FinanceStartup>();

        AddFeatures(services);

        return services;
    }

    /// <summary>
    /// Обработчики и запросы слайсов. Регистрируются поимённо, а не поиском
    /// по сборке: компоновщик Android вырезает типы, к которым нет обращений
    /// в коде, и найденный отражением обработчик исчез бы только в релизе.
    /// </summary>
    private static void AddFeatures(IServiceCollection services)
    {
        services.AddSingleton<IAccountsQuery, AccountsQuery>();
        services.AddSingleton<ISettingsSummaryQuery, SettingsSummaryQuery>();
        services.AddSingleton<IChangeThemeHandler, ChangeThemeHandler>();
        services.AddSingleton<IChangeTimeZoneHandler, ChangeTimeZoneHandler>();
        services.AddSingleton<IPlacesQuery, PlacesQuery>();
        services.AddSingleton<IRenamePlaceHandler, RenamePlaceHandler>();
        services.AddSingleton<IDeletePlaceHandler, DeletePlaceHandler>();
        services.AddSingleton<ICategoriesQuery, CategoriesQuery>();
        services.AddSingleton<ISaveCategoryHandler, SaveCategoryHandler>();
        services.AddSingleton<ICategoryDeletionQuery, CategoryDeletionQuery>();
        services.AddSingleton<IDeleteSubcategoryHandler, DeleteSubcategoryHandler>();
        services.AddSingleton<IReorderAccountsHandler, ReorderAccountsHandler>();
        services.AddSingleton<IAccountCardQuery, AccountCardQuery>();
        services.AddSingleton<ISaveAccountHandler, SaveAccountHandler>();
        services.AddSingleton<IDeleteAccountHandler, DeleteAccountHandler>();
        services.AddSingleton<IFeedQuery, FeedQuery>();
        services.AddSingleton<ITransactionFormQuery, TransactionFormQuery>();
        services.AddSingleton<ITransactionCardQuery, TransactionCardQuery>();
        services.AddSingleton<ISaveTransactionHandler, SaveTransactionHandler>();
        services.AddSingleton<IDeleteTransactionsHandler, DeleteTransactionsHandler>();
        services.AddSingleton<ITransactionDeletionQuery, TransactionDeletionQuery>();
        services.AddSingleton<IReportQuery, ReportQuery>();
        services.AddSingleton<IFrequentCategoriesQuery, FrequentCategoriesQuery>();
        services.AddSingleton<IExportHandler, ExportHandler>();
        services.AddSingleton<IRecoveryHandler, RecoveryHandler>();

        // Один на приложение: сюда экран выбора кладёт решение, а форма операции
        // забирает его при возвращении. Экраны при этом живут порознь
        services.AddSingleton<TransactionPicks>();
        services.AddSingleton<AccountBadgeDraft>();

        // Модель представления живёт ровно столько, сколько экран: общая на всё
        // приложение держала бы в памяти списки закрытых экранов
        services.AddTransient<BalancesViewModel>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<AccountViewModel>();
        services.AddTransient<AccountBadgeViewModel>();
        services.AddTransient<FeedViewModel>();
        services.AddTransient<TransactionViewModel>();
        services.AddTransient<AccountPickerViewModel>();
        services.AddTransient<CategoryPickerViewModel>();
        services.AddTransient<PlacePickerViewModel>();
        services.AddTransient<PlacesViewModel>();
        services.AddTransient<PlaceViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<SubcategoryViewModel>();
        services.AddTransient<MoreViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<ReportGroupViewModel>();
        services.AddTransient<ReportSubcategoryViewModel>();
        services.AddTransient<AppearanceViewModel>();
        services.AddTransient<TimeZoneViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<ExportViewModel>();
    }
}
