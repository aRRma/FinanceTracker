using Android.App;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Android.Window;
using AndroidX.Core.View;
using Finance.App.Controls;
using Finance.Application.Features.AppLock;
using Microsoft.Maui.Platform;

namespace Finance.App;

/// <summary>
/// Заслонка — своё окно поверх всех окон приложения, а не страница навигации:
/// страница легла бы под открытый вопрос «Удалить?», и его можно было бы нажать без входа.
/// </summary>
/// <remarks>
/// Окно — полноэкранный <see cref="Dialog"/>: диалоги приложения — такие же окна,
/// и поднятый позже ложится поверх них. При уходе в фон поднимается не оно, а шторка
/// цветом атмосферы внутри окна приложения (<see cref="Shade"/>): окно, поднятое заранее
/// и убранное при возврате, система показывала до конца анимации открытия, и заслонка
/// мелькала у того, кому вход не нужен.
/// </remarks>
internal sealed class LockCover
{
    private readonly Activity _activity;
    private readonly LockCoverViewModel _model;

    private Dialog? _dialog;
    private LockCoverView? _view;
    private Android.Views.View? _shade;
    private bool _leaving;

    /// <summary>
    /// Создаёт заслонку активности.
    /// </summary>
    /// <param name="activity">Активность приложения.</param>
    /// <param name="model">Модель заслонки.</param>
    public LockCover(Activity activity, LockCoverViewModel model)
    {
        _activity = activity;
        _model = model;

        _model.Unlocked += OnUnlocked;
    }

    /// <summary>
    /// Заслонка на экране.
    /// </summary>
    public bool IsRaised => _dialog is { IsShowing: true };

    /// <summary>
    /// Поднимает заслонку с чистым набором. Уже поднятая остаётся как есть — с набранным.
    /// </summary>
    public void Raise() => Show(fresh: true);

    /// <summary>
    /// Поднимает окно заслонки.
    /// </summary>
    /// <param name="fresh">Набор с чистого листа; иначе набранное остаётся — пересборка при смене темы.</param>
    private void Show(bool fresh)
    {
        if (IsRaised || _activity.IsFinishing || ControlsApplication.Current?.Windows is not [{ Handler.MauiContext: { } context }, ..])
        {
            return;
        }

        // Окно, закрывшееся помимо нас, отпускается: его набор остался бы подписан на модель
        Release();

        // Содержимое — каждый раз новое. Смену темы MAUI доносит до привязок
        // по дереву от приложения, а у содержимого чужого окна родителя нет:
        // прежнее оставалось в теме, при которой его создали, и хранило клавиши
        // нажатыми с прошлого входа
        LockCoverView view = new(_model);

        _view = view;

        if (fresh)
        {
            view.Prepare();
        }
        else
        {
            view.Resume();
        }

        // Содержимое MAUI в чужом окне: тот же контекст, что у окна приложения, —
        // ресурсы темы и обработчики контролов общие
        Android.Views.View content = view.ToPlatform(context);

        // Окно от края до края, а раздавать отступы под системные полосы MAUI
        // умеет только в своём окне: в диалоге клавиатура уходила под жестовую
        // полосу. Отступы получает набор, а атмосфера остаётся под полосами
        FrameLayout frame = new(_activity);
        frame.AddView(content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        ViewCompat.SetOnApplyWindowInsetsListener(frame, new BarsInset(view));

        Dialog dialog = new(_activity, Android.Resource.Style.ThemeDeviceDefaultNoActionBar);

        dialog.SetCancelable(false);
        dialog.SetContentView(frame);
        dialog.OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(
            IOnBackInvokedDispatcher.PriorityDefault,
            new BackToHome(_activity));

        if (dialog.Window is { } window)
        {
            window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);

            // Без анимации появления: при возврате из фона первым кадром
            // обязана быть заслонка целиком, а не наплывающая на балансы
            window.SetWindowAnimations(0);

            // Окно с прозрачностью с самого начала: после верного кода заслонка
            // растворяется, и под ней виден экран приложения. Сменённый на ходу
            // формат пересоздал бы поверхность окна, и уход мигнул бы. Пока
            // заслонка на месте, её фон непрозрачен, и разницы не видно
            window.SetFormat(Android.Graphics.Format.Translucent);
            window.ClearFlags(WindowManagerFlags.DimBehind);

            Paint(window);
        }

        _dialog = dialog;

        dialog.Show();
    }

    /// <summary>
    /// Закрывает содержимое окна приложения шторкой цветом атмосферы — при уходе в фон.
    /// Последний кадр окна, который система покажет при возврате, — шторка, а не балансы.
    /// </summary>
    /// <remarks>
    /// Шторка — вид внутри окна приложения, а не своё окно: снятая при возврате,
    /// она уходит со следующим кадром, а окно система убрала бы только после анимации открытия.
    /// </remarks>
    public void Shade()
    {
        if (_shade is not null || _activity.Window?.DecorView is not ViewGroup decor)
        {
            return;
        }

        // Шторка — тот же переход, что у атмосферы: при возврате она сменяется
        // заслонкой без скачка цвета, проступают только пятна
        Android.Views.View shade = new(_activity) { Background = Atmosphere() };

        decor.AddView(shade, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        _shade = shade;
    }

    /// <summary>
    /// Убирает заслонку и шторку: вход выполнен или вернулись раньше пяти минут.
    /// </summary>
    public void Dismiss()
    {
        Unshade();
        Release();
    }

    private void Unshade()
    {
        if (_shade is { } shade)
        {
            (shade.Parent as ViewGroup)?.RemoveView(shade);

            _shade = null;
        }
    }

    /// <summary>
    /// Активность уничтожается: окно убирается, а модель, одна на приложение,
    /// перестаёт держать эту заслонку — и с ней активность.
    /// </summary>
    public void Detach()
    {
        _model.Unlocked -= OnUnlocked;

        Dismiss();
    }

    /// <summary>
    /// Тема сменилась — например, системная, пока приложение в фоне. Шторка
    /// перекрашивается, а поднятая заслонка собирается заново с тем же набором:
    /// её содержимое смены темы не видит.
    /// </summary>
    public void Repaint()
    {
        _shade?.Background = Atmosphere();

        // Уходящую после верного кода заслонку не пересобирать: новая поднялась бы
        // для уже открытой модели и осталась бы поверх приложения, а уход закрыл бы
        // не своё окно
        if (IsRaised && !_leaving)
        {
            Release();
            Show(fresh: false);
        }
    }

    private void OnUnlocked(object? sender, EventArgs e) => Guarded.Run(LeaveAsync);

    /// <summary>
    /// Верный код: заслонка показывает, что замок открыт, и растворяется над экраном
    /// приложения. С выключенными в системе анимациями уходит сразу.
    /// </summary>
    private async Task LeaveAsync()
    {
        _leaving = true;

        try
        {
            if (_view is { } view
                && _dialog?.Window?.DecorView is { } decor
                && Motion.IsOn)
            {
                await view.LeaveAsync(() =>
                {
                    // Под растворяющейся заслонкой — экран приложения, а не шторка
                    // и не подложка окна
                    Unshade();
                    decor.SetBackgroundColor(Android.Graphics.Color.Transparent);
                });
            }
        }
        finally
        {
            // Сбой анимации не оставляет заслонку: касаний она уже не принимает,
            // а вход выполнен
            _leaving = false;
            Dismiss();
        }
    }

    /// <summary>
    /// Убирает окно заслонки, не трогая шторку: под поднимаемым окном она
    /// закрывает балансы, пока окно не нарисовано.
    /// </summary>
    private void Release()
    {
        _dialog?.Dismiss();
        _dialog = null;

        if (_view is { } view)
        {
            // Модель одна на приложение: подписанный на неё набор пережил бы своё окно
            view.BindingContext = null;
            view.DisconnectHandlers();

            _view = null;
        }
    }

    /// <summary>
    /// Подложка окна и значки строки состояния — по теме приложения, как у основного окна.
    /// </summary>
    private static void Paint(Android.Views.Window window)
    {
        if (ControlsApplication.Current is not { } application || window.DecorView is not { } decor)
        {
            return;
        }

        bool night = application.RequestedTheme is AppTheme.Dark;

        // Подложка — переход атмосферы: её видно первым кадром, пока MAUI не нарисовал содержимое
        decor.Background = Atmosphere();

        WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(window, decor);

        controller?.AppearanceLightStatusBars = !night;
        controller?.AppearanceLightNavigationBars = !night;
        window.NavigationBarContrastEnforced = false;
    }

    /// <summary>
    /// Переход атмосферы в нынешней теме приложения — тот же, что у <see cref="Backdrop"/>,
    /// только без пятен. Без токенов — непрозрачный чёрный, а не пустота.
    /// </summary>
    /// <remarks>
    /// Окно заслонки прозрачно, и шторка с подложкой — единственное, что закрывает балансы:
    /// опечатка в палитре не должна их открыть.
    /// </remarks>
    private static Drawable Atmosphere()
    {
        string theme = ControlsApplication.Current?.RequestedTheme is AppTheme.Dark ? "Dark" : "Light";

        if (Backdrop.Tones(theme) is not { } tones)
        {
            return new ColorDrawable(Android.Graphics.Color.Black);
        }

        GradientDrawable drawable = new();

        drawable.SetOrientation(GradientDrawable.Orientation.TopBottom);
        drawable.SetColors([.. tones.Select(static tone => tone.ToPlatform().ToArgb())], [0f, Backdrop.MiddleStop, 1f]);

        return drawable;
    }

    /// <summary>
    /// Отступы набора — под строку состояния и полосу навигации. Ставятся набору,
    /// а не рамке окна: атмосфера под ним уходит под полосы от края до края.
    /// </summary>
    private sealed class BarsInset(LockCoverView cover) : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? view, WindowInsetsCompat? insets)
        {
            if (view is not null && insets?.GetInsets(WindowInsetsCompat.Type.SystemBars()) is { } bars)
            {
                // Отступы приходят в точках экрана, а MAUI меряет в независимых
                double density = view.Resources?.DisplayMetrics?.Density ?? 1;

                cover.Inset(new Thickness(bars.Left / density, bars.Top / density, bars.Right / density, bars.Bottom / density));
            }

            return WindowInsetsCompat.Consumed;
        }
    }

    /// <summary>
    /// «Назад» на заслонке сворачивает приложение: закрыть экран под ней без входа нельзя.
    /// </summary>
    private sealed class BackToHome(Activity activity) : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked() => activity.MoveTaskToBack(true);
    }
}
