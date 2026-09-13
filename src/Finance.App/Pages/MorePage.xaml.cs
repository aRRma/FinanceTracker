namespace Finance.App.Pages;

/// <summary>Экран A-04: справочники и настройки.</summary>
public partial class MorePage : ContentPage
{
    /// <summary>Создаёт экран.</summary>
    public MorePage()
    {
        InitializeComponent();
    }

    private async void OnAccounts(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(Routes.Accounts);
}
