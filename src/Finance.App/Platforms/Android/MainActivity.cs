using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

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
    /// <summary>Действие намерения ярлыка. Совпадает с описанием ярлыков в ресурсах.</summary>
    private const string AddTransaction = "ru.finance.tracker.action.ADD_TRANSACTION";

    /// <summary>Холодный старт: приложения не было, ярлык поднял его с нуля.</summary>
    /// <param name="savedInstanceState">Состояние, сохранённое платформой.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Accept(Intent);
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
