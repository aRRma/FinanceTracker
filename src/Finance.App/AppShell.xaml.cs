using Finance.Domain.Enums;

namespace Finance.App;

/// <summary>
/// Каркас навигации: четыре вкладки и маршруты вложенных экранов.
/// </summary>
public sealed partial class AppShell : Shell
{
    // Вкладка, с которой уходят. В OnNavigated текущая вкладка уже новая,
    // а сбрасывать нужно стек прежней
    private ShellSection? _leaving;

    /// <summary>
    /// Создаёт каркас и регистрирует маршруты.
    /// </summary>
    public AppShell()
    {
        InitializeComponent();

        Routes.Register();
    }

    /// <summary>
    /// Открывает форму новой операции выбранного вида — этим приходят с ярлыка
    /// на значке приложения. Маршрут абсолютный: он сбрасывает стек вкладки
    /// и кладёт форму поверх балансов, поэтому «назад» с неё ведёт на главный
    /// экран, а не на то, что пользователь смотрел неделю назад.
    /// </summary>
    /// <param name="kind">Вид операции.</param>
    internal Task OpenTransactionAsync(TransactionKind kind) =>
        GoToAsync($"//balances/{Routes.Transaction}?kind={kind}");

    /// <inheritdoc />
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        ArgumentNullException.ThrowIfNull(args);

        _leaving = args.Source is ShellNavigationSource.ShellSectionChanged
            ? CurrentItem?.CurrentItem
            : null;
    }

    /// <summary>
    /// Вкладка всегда открывается своим корнем. Стек прежней вкладки живёт
    /// иначе неделями: пользователь, ушедший с ленты счёта на «Операции»,
    /// вернувшись на «Балансы», видел бы не балансы, а ту же ленту — и не
    /// понимал бы, почему нижняя панель не сработала.
    /// </summary>
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        ArgumentNullException.ThrowIfNull(args);

        // Одно место на все переходы, включая «назад» и смену вкладки. Без адреса экран в следе не меняется
        if (args.Current?.Location?.OriginalString is { Length: > 0 } location)
        {
            CrashCatcher.Trail?.Navigated(location);
        }

        // Ярлык на значке поднимает приложение раньше, чем появляется каркас,
        // и запрос ждёт здесь — первой навигации, после которой есть куда идти
        ShortcutLaunch.Deliver();

        if (args.Source is not ShellNavigationSource.ShellSectionChanged || _leaving is not { } leaving)
        {
            return;
        }

        _leaving = null;

        // Первый элемент стека — корневая страница вкладки, и сбрасывать
        // нечего, пока над ней ничего не лежит
        if (leaving.Navigation.NavigationStack.Count > 1)
        {
            Guarded.Run(() => leaving.Navigation.PopToRootAsync(animated: false));
        }
    }
}
