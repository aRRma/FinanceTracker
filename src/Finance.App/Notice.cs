using Android.Widget;

namespace Finance.App;

/// <summary>
/// Короткое сообщение внизу экрана, которое уходит само. Для того, что пользователю
/// нужно знать, но отвечать на что нечего: диалог с кнопкой прерывал бы ввод.
/// </summary>
internal static class Notice
{
    /// <summary>
    /// Показывает сообщение. Зовётся из потока интерфейса.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    internal static void Show(string text) =>
        Toast.MakeText(Platform.AppContext, text, ToastLength.Long)?.Show();
}
