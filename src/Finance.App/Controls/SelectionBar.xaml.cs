using Finance.Application.Features.Feed;

namespace Finance.App.Controls;

/// <summary>
/// Шапка ленты при выделении: встаёт на место заголовка страницы, пока выделение включено.
/// </summary>
public sealed partial class SelectionBar : ContentView
{
    /// <summary>
    /// Создаёт шапку.
    /// </summary>
    public SelectionBar() => InitializeComponent();

    /// <summary>
    /// Список, чьё выделение удаляется. Задаёт страница: шапка стоит в панели
    /// заголовка, а список — в содержимом, и ссылкой из разметки до него не дотянуться.
    /// </summary>
    public FeedList? List { get; set; }

    private void OnEnd(object? sender, TappedEventArgs e) => (BindingContext as FeedViewModel)?.EndSelection();

    private void OnDelete(object? sender, EventArgs e) => List?.DeleteSelected();
}
