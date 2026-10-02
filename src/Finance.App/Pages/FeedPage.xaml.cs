using System.ComponentModel;
using Finance.App.Controls;
using Finance.Application.Features.Feed;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран A-02: общая лента операций по всем счетам.
/// </summary>
public sealed partial class FeedPage : DataPage
{
    private readonly FeedViewModel _model;

    private readonly SelectionBar _selection;

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

        // Кнопка объявлена в разметке после списка: ссылкой вперёд её не достать
        List.Floating = AddButton;
        _selection = new SelectionBar { List = List };

        _model = model;
        BindingContext = model;

        // Модель живёт ровно столько, сколько страница, — отписка не нужна
        model.PropertyChanged += OnModelChanged;
    }

    /// <summary>
    /// При выделении место заголовка занимает шапка выделения. Ставится только на
    /// это время, а не панелью навсегда: заголовок вкладки рисует система, и свой
    /// заголовок отличался бы шрифтом от заголовков соседних вкладок.
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FeedViewModel.IsSelecting))
        {
            Shell.SetTitleView(this, _model.IsSelecting ? _selection : null);
        }
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
