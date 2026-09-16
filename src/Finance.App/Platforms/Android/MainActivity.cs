using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;

namespace Finance.App;

/// <summary>
/// Единственная активность приложения. Кроме запуска принимает намерения ярлыков
/// с значка: они несут вид операции, которую пользователь хочет записать.
/// </summary>
/// <remarks>
/// Имя класса в системе задано явно: без него платформа собирает имя сама, дописывая
/// к нему хеш сборки, и сослаться на активность из описания ярлыков было бы нечем.
/// </remarks>
[Activity(
    Name = "ru.finance.tracker.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Действие намерения ярлыка. Совпадает с описанием ярлыков в ресурсах.
    /// </summary>
    private const string AddTransaction = "ru.finance.tracker.action.ADD_TRANSACTION";

    /// <summary>
    /// Холодный старт: приложения не было, ярлык поднял его с нуля.
    /// </summary>
    /// <param name="savedInstanceState">Состояние, сохранённое платформой.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Accept(Intent);

        if (ControlsApplication.Current is { } application)
        {
            application.RequestedThemeChanged += (_, _) => ApplyStatusBar();
        }
    }

    /// <summary>
    /// Каркас навигации красит строку состояния сам при первом показе, и цвет
    /// ставится после него — когда окно получило фокус.
    /// </summary>
    /// <param name="hasFocus">Окно в фокусе.</param>
    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);

        if (hasFocus)
        {
            ApplyStatusBar();
        }
    }

    /// <summary>
    /// Смена темы приходит сюда изменением конфигурации, а не пересозданием
    /// активности: <c>UiMode</c> перечислен в <c>ConfigurationChanges</c>.
    /// </summary>
    /// <param name="newConfig">Новая конфигурация.</param>
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        ApplyStatusBar();
    }

    /// <summary>
    /// Строка состояния — цветом шапки, значки на ней тёмные на светлой и светлые
    /// на тёмной. Ресурсом это не задать: тема, выбранная в настройках, ночной
    /// режим платформы не переключает, и <c>values-night</c> её не увидит. Цвет
    /// берётся из палитры приложения, чтобы шапка и строка над ней не разошлись.
    /// </summary>
    private void ApplyStatusBar()
    {
        if (Window is not { DecorView: { } decor } window || ControlsApplication.Current is not { } application)
        {
            return;
        }

        bool night = application.RequestedTheme is AppTheme.Dark;

        if (application.Resources.TryGetValue(night ? "CardDark" : "CardLight", out object? value) && value is Color card)
        {
            // Окно от края до края: строка состояния прозрачна, и под ней видна подложка
            // окна. Контейнер шапки над ней прозрачный (styles.xml), иначе он закрыл бы её
            decor.SetBackgroundColor(card.ToPlatform());
        }

        WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(window, decor);

        controller?.AppearanceLightStatusBars = !night;
    }

    /// <summary>
    /// Ярлык нажали при живом приложении. Второй активности при этом не создаётся:
    /// режим запуска <c>SingleTop</c> отдаёт намерение существующей.
    /// </summary>
    /// <param name="intent">Намерение ярлыка.</param>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // Иначе прежнее намерение осталось бы у активности и сработало бы
        // ещё раз при следующем повороте экрана
        Intent = intent;

        Accept(intent);
    }

    private static void Accept(Intent? intent)
    {
        if (intent?.Action != AddTransaction)
        {
            return;
        }

        if (ShortcutLaunch.Parse(intent.GetStringExtra("kind")) is { } kind)
        {
            ShortcutLaunch.Request(kind);
        }
    }
}
