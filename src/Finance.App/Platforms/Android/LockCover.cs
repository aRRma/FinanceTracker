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
/// и поднятый позже ложится поверх них. Поднимается оно при уходе в фон, чтобы
/// при возврате первым кадром была заслонка, а не балансы.
/// </remarks>
internal sealed class LockCover
{
    private readonly Activity _activity;
    private readonly LockCoverViewModel _model;

    private Dialog? _dialog;
    private LockCoverView? _view;

    /// <summary>
    /// Создаёт заслонку активности.
    /// </summary>
    /// <param name="activity">Активность приложения.</param>
    /// <param name="model">Модель заслонки.</param>
    public LockCover(Activity activity, LockCoverViewModel model)
    {
        _activity = activity;
        _model = model;

        _model.Unlocked += (_, _) => Dismiss();
    }

    /// <summary>
    /// Заслонка на экране.
    /// </summary>
    public bool IsRaised => _dialog is { IsShowing: true };

    /// <summary>
    /// Поднимает заслонку. Уже поднятая остаётся как есть — с набранным.
    /// </summary>
    public void Raise()
    {
        if (IsRaised || _activity.IsFinishing || ControlsApplication.Current?.Windows is not [{ Handler.MauiContext: { } context }, ..])
        {
            return;
        }

        _view ??= new LockCoverView(_model);
        _view.Prepare();

        // Содержимое MAUI в чужом окне: тот же контекст, что у окна приложения, —
        // ресурсы темы и обработчики контролов общие
        Android.Views.View content = _view.ToPlatform(context);

        (content.Parent as ViewGroup)?.RemoveView(content);

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

        dialog.Show();

        _dialog = dialog;
    }

    /// <summary>
    /// Убирает заслонку: вход выполнен или вернулись раньше пяти минут.
    /// </summary>
    public void Dismiss()
    {
        _dialog?.Dismiss();
        _dialog = null;
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

        if (application.Resources.TryGetValue(night ? "PaperDark" : "PaperLight", out object? value) && value is Color paper)
        {
            decor.SetBackgroundColor(paper.ToPlatform());
        }

        WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(window, decor);

        controller?.AppearanceLightStatusBars = !night;
        controller?.AppearanceLightNavigationBars = !night;
        window.NavigationBarContrastEnforced = false;
    }

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
