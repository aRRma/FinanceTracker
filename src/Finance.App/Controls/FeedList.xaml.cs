using Finance.Application.Features.Feed;

namespace Finance.App.Controls;

/// <summary>Список ленты: общий для вкладки операций и ленты счёта.</summary>
public partial class FeedList : ContentView
{
    /// <summary>Создаёт список.</summary>
    public FeedList()
    {
        InitializeComponent();
    }

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: FeedRowItem row })
        {
            await Shell.Current.GoToAsync($"{Routes.Transaction}?key={row.Key}");
        }
    }
}
