using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;
using Finance.Application.Features.AppLock;
using Finance.Application.Infrastructure.AppLock;
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
    Theme = "@style/Finance.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
public sealed class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Действие намерения ярлыка. Совпадает с описанием ярлыков в ресурсах.
    /// </summary>
    private const string AddTransaction = "ru.finance.tracker.action.ADD_TRANSACTION";

    private AppLockService? _appLock;
    private LockCover? _cover;

    /// <summary>
    /// Холодный старт: приложения не было, ярлык поднял его с нуля.
    /// </summary>
    /// <param name="savedInstanceState">Состояние, сохранённое платформой.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (IPlatformApplication.Current?.Services is { } services)
        {
            _appLock = services.GetRequiredService<AppLockService>();
            _cover = new LockCover(this, services.GetRequiredService<LockCoverViewModel>());

            // До первого кадра каркаса: при запуске балансы не мелькают даже на миг.
            // Пересозданная в живом процессе активность решает как возврат из фона.
            // Ярлык при этом открывает форму под заслонкой — пропуска входа
            // через намерение нет: подделать его могло бы любое приложение
            if (_appLock.Start())
            {
                _cover.Raise();
            }
        }

        Accept(Intent);

        if (ControlsApplication.Current is { } application)
        {
            application.RequestedThemeChanged += OnThemeChanged;
        }
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ApplyStatusBar();
        _cover?.Repaint();
    }

    /// <summary>
    /// Миниатюра в списке недавних скрыта, пока защита включена. Решается при
    /// каждом выходе на экран: защиту включают и выключают на ходу. Заслонка,
    /// которую не удалось поднять раньше, поднимается здесь же: поднятый признак
    /// входа без заслонки оставил бы балансы открытыми.
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();

        SetRecentsScreenshotEnabled(_appLock?.IsEnabled is not true);

        if (_appLock?.IsLocked is true)
        {
            _cover?.Raise();
        }
    }

    /// <summary>
    /// Признак миниатюры обновляется и перед уходом с экрана: защиту включили
    /// на ходу, и снимок для списка недавних делается раньше следующего показа.
    /// </summary>
    protected override void OnPause()
    {
        base.OnPause();

        SetRecentsScreenshotEnabled(_appLock?.IsEnabled is not true);
    }

    /// <summary>
    /// Уход в фон: отсчёт времени в фоне и шторка заранее — при возврате первым
    /// кадром будет она, а не балансы. Заслонку решает <see cref="OnRestart"/>:
    /// поднятую здесь при возврате раньше пяти минут было бы видно всю анимацию открытия.
    /// </summary>
    protected override void OnStop()
    {
        base.OnStop();

        _appLock?.Leave();

        if (_appLock?.IsEnabled is true)
        {
            _cover?.Shade();
        }
    }

    /// <summary>
    /// Окно заслонки принадлежит этой активности: оставленное при её уничтожении,
    /// оно утекло бы. Новая активность поднимет свою, если вход нужен. Приложение
    /// и модель заслонки живут дольше активности и отпускают её здесь же.
    /// </summary>
    protected override void OnDestroy()
    {
        ControlsApplication.Current?.RequestedThemeChanged -= OnThemeChanged;

        _cover?.Detach();

        base.OnDestroy();
    }

    /// <summary>
    /// Возврат из фона: заслонка поднимается поверх шторки, если вход нужен,
    /// иначе шторка убирается.
    /// </summary>
    protected override void OnRestart()
    {
        base.OnRestart();

        if (_appLock?.Return() is true)
        {
            // Уже поднятая остаётся как есть — с набранным
            _cover?.Raise();
        }
        else
        {
            _cover?.Dismiss();
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
    /// Строка состояния — цветом шапки, значки на ней и на панели навигации тёмные
    /// на светлой и светлые на тёмной. Ресурсом это не задать: тема, выбранная в настройках, ночной
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

        if (Palette.Now("Card") is { } card)
        {
            // Окно от края до края: строка состояния прозрачна, и под ней видна подложка
            // окна. Контейнер шапки над ней прозрачный (styles.xml), иначе он закрыл бы её
            decor.SetBackgroundColor(card.ToPlatform());
        }

        WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(window, decor);

        controller?.AppearanceLightStatusBars = !night;

        // Трёхкнопочная панель навигации: своей подложки для контраста у неё нет —
        // система рисовала светлую и в тёмной теме приложения, — под кнопками видна
        // панель вкладок или подложка окна, а значки красятся по теме, как на строке состояния
        window.NavigationBarContrastEnforced = false;
        controller?.AppearanceLightNavigationBars = !night;
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
#if DEBUG
        DebugCrash.Accept(intent);
#endif

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
