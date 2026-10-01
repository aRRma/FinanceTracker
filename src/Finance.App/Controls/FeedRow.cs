using AView = Android.Views.View;

namespace Finance.App.Controls;

/// <summary>
/// Строка ленты, понимающая касание и долгое нажатие. Оба жеста — платформенные
/// касание и долгое нажатие вида Android, а не распознаватели MAUI.
/// </summary>
/// <remarks>
/// Распознаватель касания MAUI вешает на вид свой обработчик касаний, и тот спорил бы
/// с долгим нажатием за одни и те же события. Платформенная пара разводит их сама:
/// долгое нажатие гасит касание, прокрутка списка и смахивание строки отменяют оба,
/// а отклик вибрацией на долгое нажатие система даёт без нашего кода. Встроенный
/// распознаватель долгого нажатия появится в MAUI 11 — тогда эта строка не нужна.
/// </remarks>
public sealed class FeedRow : Grid
{
    /// <summary>
    /// Строку коснулись: открыть операцию или, при выделении, отметить её.
    /// </summary>
    public event EventHandler? Tapped;

    /// <summary>
    /// На строке задержали палец: включить выделение.
    /// </summary>
    public event EventHandler? LongPressed;

    /// <inheritdoc />
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // Список переиспользует строки: подписка, оставшаяся на старом виде,
        // сработала бы на чужой строке и удержала бы её в памяти
        if (args.OldHandler?.PlatformView is AView old)
        {
            old.Click -= OnClick;
            old.LongClick -= OnLongClick;
        }
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Подписка — здесь, а не при смене обработчика: там платформенного вида ещё нет
        if (Handler?.PlatformView is AView view)
        {
            view.Click -= OnClick;
            view.Click += OnClick;
            view.LongClick -= OnLongClick;
            view.LongClick += OnLongClick;
        }
    }

    private void OnClick(object? sender, EventArgs e) => Tapped?.Invoke(this, EventArgs.Empty);

    private void OnLongClick(object? sender, AView.LongClickEventArgs e)
    {
        // Обработано: иначе за долгим нажатием пришло бы ещё и касание
        e.Handled = true;

        LongPressed?.Invoke(this, EventArgs.Empty);
    }
}
