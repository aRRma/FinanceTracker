using Finance.Application.Features.Feed;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран A-03: лента одного счёта.
/// </summary>
[QueryProperty(nameof(Key), "key")]
public partial class AccountFeedPage : DataPage
{
    private readonly FeedViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления ленты.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AccountFeedPage(FeedViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Ключ счёта из маршрута. Строкой, а не <see cref="Guid"/>: в маршруте он и есть строка.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Только устаревшая лента: перечитывание возвращает прокрутку к началу, и
    /// заглянувший в операцию из глубины истории терял бы место, с которого ушёл.
    /// </summary>
    protected override bool ReloadsOnAppearing => _model.IsOutdated;

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        Guid.TryParse(Key, out Guid key) ? _model.LoadAsync(key) : Task.CompletedTask;

    private void OnAdd(object? sender, EventArgs e) =>
        Navigator.Go($"{Routes.Transaction}?account={Key}");

    // Правка счёта — с его ленты: из «Ещё → Счета» вход искать никто не станет
    private void OnEdit(object? sender, TappedEventArgs e) =>
        Navigator.Go($"{Routes.Account}?key={Key}");
}
