using Finance.Application.Features.Feed;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран A-02: общая лента операций по всем счетам.
/// </summary>
public partial class FeedPage : DataPage
{
    private readonly FeedViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления ленты.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public FeedPage(FeedViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>
    /// Только устаревшая лента: перечитывание возвращает прокрутку к началу, и
    /// заглянувший в операцию из глубины истории терял бы место, с которого ушёл.
    /// </summary>
    protected override bool ReloadsOnAppearing => _model.IsOutdated;

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync(accountKey: null);

    private void OnAdd(object? sender, EventArgs e) =>
        Navigator.Go(Routes.Transaction);
}
