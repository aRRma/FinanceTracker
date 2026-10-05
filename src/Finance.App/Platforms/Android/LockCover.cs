using Android.App;
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
/// цветом фона внутри окна приложения (<see cref="Shade"/>): окно, поднятое заранее
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
        // полосу. Отступы ставит рамка вокруг содержимого
        FrameLayout frame = new(_activity);
        frame.AddView(content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        ViewCompat.SetOnApplyWindowInsetsListener(frame, new BarsPadding());

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

            Paint(window);
        }

        _dialog = dialog;

        dialog.Show();
    }

    /// <summary>
    /// Закрывает содержимое окна приложения шторкой цветом фона — при уходе в фон.
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

        Android.Views.View shade = new(_activity);

        if (Paper() is { } paper)
        {
            shade.SetBackgroundColor(paper.ToPlatform());
        }

        decor.AddView(shade, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        _shade = shade;
    }

    /// <summary>
    /// Убирает заслонку и шторку: вход выполнен или вернулись раньше пяти минут.
    /// </summary>
    public void Dismiss()
    {
        if (_shade is { } shade)
        {
            (shade.Parent as ViewGroup)?.RemoveView(shade);

            _shade = null;
        }

        Release();
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
        if (_shade is { } shade && Paper() is { } paper)
        {
            shade.SetBackgroundColor(paper.ToPlatform());
        }

        if (IsRaised)
        {
            Release();
            Show(fresh: false);
        }
    }

    private void OnUnlocked(object? sender, EventArgs e) => Dismiss();

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

        if (Paper() is { } paper)
        {
            decor.SetBackgroundColor(paper.ToPlatform());
        }

        WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(window, decor);

        controller?.AppearanceLightStatusBars = !night;
        controller?.AppearanceLightNavigationBars = !night;
        window.NavigationBarContrastEnforced = false;
    }

    /// <summary>
    /// Цвет фона страниц в нынешней теме приложения.
    /// </summary>
    private static Color? Paper() =>
        ControlsApplication.Current is { } application
        && application.Resources.TryGetValue(application.RequestedTheme is AppTheme.Dark ? "PaperDark" : "PaperLight", out object? value)
            ? value as Color
            : null;

    /// <summary>
    /// Отступы рамки — под строку состояния и полосу навигации.
    /// </summary>
    private sealed class BarsPadding : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? view, WindowInsetsCompat? insets)
        {
            if (insets?.GetInsets(WindowInsetsCompat.Type.SystemBars()) is { } bars)
            {
                view?.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
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
