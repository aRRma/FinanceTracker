namespace Finance.App;

/// <summary>Каркас навигации: четыре вкладки и маршруты вложенных экранов.</summary>
public partial class AppShell : Shell
{
    /// <summary>Создаёт каркас и регистрирует маршруты.</summary>
    public AppShell()
    {
        InitializeComponent();

        Routes.Register();
    }
}
