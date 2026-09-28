using Finance.Application.Features.Feed;

namespace Finance.App.Controls;

/// <summary>
/// Список ленты: общий для вкладки операций и ленты счёта.
/// </summary>
public partial class FeedList : ContentView
{
    /// <summary>
    /// Чем заполнена пустая лента.
    /// </summary>
    public static readonly BindableProperty EmptyProperty =
        BindableProperty.Create(nameof(Empty), typeof(View), typeof(FeedList));

    /// <summary>
    /// Создаёт список.
    /// </summary>
    public FeedList()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Чем заполнена пустая лента. Задаёт страница: у общей ленты это приглашение
    /// записать первую операцию, у ленты счёта — ещё и строка начального остатка,
    /// иначе непонятно, откуда взялся баланс в шапке.
    /// </summary>
    public View? Empty
    {
        get => (View?)GetValue(EmptyProperty);
        set => SetValue(EmptyProperty, value);
    }

    /// <summary>
    /// Жест «потянуть вниз». Обработчиком, а не привязкой команды: сбой команды,
    /// запущенной разметкой, закрыл бы окно молча.
    /// </summary>
    private void OnRefreshing(object? sender, EventArgs e)
    {
        if (BindingContext is FeedViewModel model)
        {
            Guarded.Run(model.RefreshAsync);
        }
    }

    /// <summary>
    /// Прокрутка подошла к концу прочитанного — дочитать следующую страницу.
    /// </summary>
    private void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (BindingContext is FeedViewModel model)
        {
            Guarded.Run(() => model.LoadMoreAsync());
        }
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: FeedRowItem row })
        {
            Navigator.Go($"{Routes.Transaction}?key={row.Key}");
        }
    }
}
