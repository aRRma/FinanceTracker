using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Accounts.Catalog;
using Finance.Application.Features.Balances;
using Finance.Application.Features.Categories.Card;
using Finance.Application.Features.Categories.Catalog;
using Finance.Application.Features.Places.Card;
using Finance.Application.Features.Places.Catalog;
using Finance.Application.Features.Feed;
using Finance.Application.Features.More;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Initialization;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application;

/// <summary>
/// Сборка прикладного слоя. Приложение MAUI подключает его одним вызовом и про
/// EF Core, SQLite и пути к файлам не знает: иначе слой доступа к данным протёк бы
/// в страницы, и тесты потребовали бы эмулятор.
/// </summary>
public static class FinanceServices
{
    /// <summary>Регистрирует хранилище, часы, справочник значков и подготовку базы.</summary>
    /// <param name="services">Набор служб приложения.</param>
    /// <param name="databasePath">Полный путь к файлу базы в папке данных приложения.</param>
    /// <param name="dispatchToInterface">
    /// Как выполнить действие в потоке интерфейса. Команды выполняются в фоне,
    /// а экраны по их итогу правят привязанные коллекции — платформа обязана
    /// вернуть это в свой поток. Пусто — оповещение приходит на месте, так работают тесты.
    /// </param>
    public static IServiceCollection AddFinance(
        this IServiceCollection services,
        string databasePath,
        Action<Action>? dispatchToInterface = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        DatabaseLocation location = new(databasePath);

        services.AddSingleton(location);
        services.AddSingleton(IconCatalog.Embedded());

        // Часы регистрируются одним объектом под двумя именами: настройки меняют
        // зону через SystemClock, а читают время все остальные через IClock
        services.AddSingleton<SystemClock>();
        services.AddSingleton<IClock>(provider => provider.GetRequiredService<SystemClock>());

        services.AddDbContextFactory<FinanceDbContext>(options => options.UseSqlite(location.ConnectionString));

        services.AddSingleton<IChangeNotifier>(new ChangeNotifier(dispatchToInterface));
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
        services.AddSingleton<IFeedQuery, FeedQuery>();
        services.AddSingleton<ITransactionFormQuery, TransactionFormQuery>();
        services.AddSingleton<ITransactionCardQuery, TransactionCardQuery>();
        services.AddSingleton<ISaveTransactionHandler, SaveTransactionHandler>();
        services.AddSingleton<IDeleteTransactionHandler, DeleteTransactionHandler>();

        // Модель представления живёт ровно столько, сколько экран: общая на всё
        // приложение держала бы в памяти списки закрытых экранов
        services.AddTransient<BalancesViewModel>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<AccountViewModel>();
        services.AddTransient<FeedViewModel>();
        services.AddTransient<TransactionViewModel>();
        services.AddTransient<PlacesViewModel>();
        services.AddTransient<PlaceViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<SubcategoryViewModel>();
        services.AddTransient<MoreViewModel>();
    }
}
