using Finance.App.Pages;
using Finance.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Finance.App;

/// <summary>Сборка приложения: службы прикладного слоя, шрифты, журналирование.</summary>
public static class MauiProgram
{
    /// <summary>Имя файла базы в папке данных приложения.</summary>
    private const string DatabaseFileName = "finance.db";

    /// <summary>Собирает приложение.</summary>
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Путь к папке данных знает только платформа: прикладной слой собирается
        // и тестируется без неё и получает путь готовым
        builder.Services.AddFinance(
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName),
            dispatchToInterface: MainThread.BeginInvokeOnMainThread);

        AddPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Страницы берутся из контейнера: иначе модель представления пришлось бы
    /// создавать в конструкторе страницы руками, со всеми её зависимостями.
    /// </summary>
    private static void AddPages(IServiceCollection services)
    {
        services.AddTransient<BalancesPage>();
        services.AddTransient<FeedPage>();
        services.AddTransient<ReportPage>();
        services.AddTransient<MorePage>();
        services.AddTransient<AccountsPage>();
        services.AddTransient<AccountPage>();
        services.AddTransient<AccountFeedPage>();
        services.AddTransient<TransactionPage>();
    }
}
