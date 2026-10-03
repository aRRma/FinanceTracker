using Finance.App.Controls;
using Finance.App.Pages;
using Finance.Application;
using Finance.Application.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace Finance.App;

/// <summary>
/// Сборка приложения: службы прикладного слоя, шрифты, журналирование.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Имя файла базы в папке данных приложения.
    /// </summary>
    private const string DatabaseFileName = "finance.db";

    /// <summary>
    /// Собирает приложение.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Фигуру своего типа MAUI не находит: обработчики фигур записаны
            // поимённо, и без этой строки любой экран со значком падает при открытии
            .ConfigureMauiHandlers(handlers => handlers.AddHandler<Icon, ShapeViewHandler>())
            // Свой обработчик вкладок: без него каждый переход красит шапку чёрным
            .ConfigureMauiHandlers(handlers => handlers.AddHandler<Shell, FinanceShellRenderer>())
            // FitLabel подбирает кегль по ширине: сумма в двенадцать разрядов при крупном
            // масштабе экрана иначе не помещается в строку. Прямой размер текста, который
            // MAUI ставит по FontSize, платформа при включённом автоподборе не применяет,
            // поэтому FontSize служит его верхним пределом
            .ConfigureMauiHandlers(static _ => LabelHandler.Mapper.AppendToMapping(nameof(FitLabel), static (handler, view) =>
            {
                if (view is FitLabel fit)
                {
                    // Пределы кегля, шаг подбора в один пункт и единицы — пункты шрифта
                    AndroidX.Core.Widget.TextViewCompat.SetAutoSizeTextTypeUniformWithConfiguration(
                        handler.PlatformView,
                        fit.MinimumSize,
                        (int)Math.Max(fit.FontSize, fit.MinimumSize),
                        1,
                        (int)Android.Util.ComplexUnitType.Sp);
                }
            }))
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Путь к папке данных знает только платформа: прикладной слой собирается
        // и тестируется без неё и получает путь готовым
        builder.Services.AddFinance(
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName),
            dispatchToInterface: MainThread.BeginInvokeOnMainThread,
            applyTheme: ApplyTheme,
            applicationVersion: AppInfo.Current.VersionString,
            cacheFolder: FileSystem.CacheDirectory);

        AddPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Переключает оформление окна. Системная тема ставится явным «не задано»:
    /// MAUI помнит выбор прошлого запуска, и без сброса возврат к системной
    /// теме не подействовал бы.
    /// </summary>
    private static void ApplyTheme(Theme theme) =>
        ControlsApplication.Current?.UserAppTheme = theme switch
        {
            Theme.Light => AppTheme.Light,
            Theme.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

    /// <summary>
    /// Страницы берутся из контейнера: иначе модель представления пришлось бы
    /// создавать в конструкторе страницы руками, со всеми её зависимостями.
    /// </summary>
    private static void AddPages(IServiceCollection services)
    {
        services.AddTransient<BalancesPage>();
        services.AddTransient<FeedPage>();
        services.AddTransient<ReportPage>();
        services.AddTransient<ReportGroupPage>();
        services.AddTransient<ReportSubcategoryPage>();
        services.AddTransient<MorePage>();
        services.AddTransient<AccountsPage>();
        services.AddTransient<AccountPage>();
        services.AddTransient<AccountFeedPage>();
        services.AddTransient<PlacesPage>();
        services.AddTransient<PlacePage>();
        services.AddTransient<CategoriesPage>();
        services.AddTransient<GroupPage>();
        services.AddTransient<SubcategoryPage>();
        services.AddTransient<TransactionPage>();
        services.AddTransient<AccountPickerPage>();
        services.AddTransient<CategoryPickerPage>();
        services.AddTransient<PlacePickerPage>();
        services.AddTransient<AppearancePage>();
        services.AddTransient<TimeZonePage>();
        services.AddTransient<AboutPage>();
        services.AddTransient<ExportPage>();
    }
}
