using System.ComponentModel;
using Finance.Application.Features.Feed;
using Finance.Application.Infrastructure.Deletion;
using Finance.Application.Texts;

namespace Finance.App.Controls;

/// <summary>
/// Список ленты: общий для вкладки операций и ленты счёта.
/// </summary>
public sealed partial class FeedList : ContentView
{
    /// <summary>
    /// Сколько точек пролистать в одну сторону, прежде чем кнопка спрячется или вернётся:
    /// без порога она дёргалась бы на каждом мелком движении пальца.
    /// </summary>
    private const double ScrollThreshold = 24;

    /// <summary>
    /// Чем заполнена пустая лента.
    /// </summary>
    public static readonly BindableProperty EmptyProperty =
        BindableProperty.Create(nameof(Empty), typeof(View), typeof(FeedList));

    /// <summary>
    /// Плавающая кнопка страницы, которую список прячет при прокрутке.
    /// </summary>
    public static readonly BindableProperty FloatingProperty =
        BindableProperty.Create(nameof(Floating), typeof(View), typeof(FeedList));

    private FeedViewModel? _model;
    private Page? _page;

    // Путь прокрутки в одну сторону с последней смены направления
    private double _travel;
    private bool _floatingHidden;

    // Строка, оставленная смахнутой: её «Удалить» видно и после того, как включили
    // выделение, а там оно путало бы, что удалится
    private SwipeView? _swiped;

    // Идёт удаление: второе касание «Удалить» до конца первого открыло бы второй диалог
    private bool _deleting;

    /// <summary>
    /// Создаёт список.
    /// </summary>
    public FeedList()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
    /// Плавающая кнопка страницы. Прокрутка вниз прячет её, вверх и к началу списка —
    /// возвращает: кнопка закрывала сумму последней видимой строки, а пользователь,
    /// листающий вниз, читает, а не записывает. При выделении она спрятана тоже —
    /// там нужна другая кнопка.
    /// </summary>
    public View? Floating
    {
        get => (View?)GetValue(FloatingProperty);
        set => SetValue(FloatingProperty, value);
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        _model?.PropertyChanged -= OnModelChanged;
        _model = BindingContext as FeedViewModel;
        _model?.PropertyChanged += OnModelChanged;
    }

    // Страница известна, только когда список уже на ней: при создании родителя ещё нет
    private void OnLoaded(object? sender, EventArgs e)
    {
        _page = FindPage();
        _page?.Appearing += OnPageAppearing;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _page?.Appearing -= OnPageAppearing;
        _page = null;
    }

    // Вернувшийся на страницу видит кнопку, даже если ушёл, пролистав вниз
    private void OnPageAppearing(object? sender, EventArgs e) => ShowFloating(_model is not { IsSelecting: true });

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FeedViewModel.IsSelecting))
        {
            ShowFloating(_model is not { IsSelecting: true });

            if (_model is { IsSelecting: true })
            {
                _swiped?.Close(animated: false);
                _swiped = null;
            }
        }
    }

    /// <summary>
    /// Жест «потянуть вниз». Обработчиком, а не привязкой команды: сбой команды,
    /// запущенной разметкой, закрыл бы окно молча.
    /// </summary>
    private void OnRefreshing(object? sender, EventArgs e)
    {
        if (_model is { } model)
        {
            Guarded.Run(model.RefreshAsync);
        }
    }

    /// <summary>
    /// Прокрутка подошла к концу прочитанного — дочитать следующую страницу.
    /// </summary>
    private void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (_model is { } model)
        {
            Guarded.Run(() => model.LoadMoreAsync());
        }
    }

    /// <summary>
    /// Прячет кнопку при прокрутке вниз и возвращает при прокрутке вверх. Считается путь
    /// в одну сторону, а не каждый сдвиг: палец, лежащий на списке, двигает его
    /// на точку туда и обратно, и кнопка мигала бы.
    /// </summary>
    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (_model is { IsSelecting: true })
        {
            return;
        }

        // У начала списка кнопка видна всегда: прятать её некому мешать
        if (e.VerticalOffset < ScrollThreshold)
        {
            _travel = 0;
            ShowFloating(true);

            return;
        }

        if (Math.Sign(e.VerticalDelta) != Math.Sign(_travel))
        {
            _travel = 0;
        }

        _travel += e.VerticalDelta;

        if (Math.Abs(_travel) >= ScrollThreshold)
        {
            ShowFloating(_travel < 0);
        }
    }

    private void ShowFloating(bool visible)
    {
        if (Floating is not { } floating || _floatingHidden == !visible)
        {
            return;
        }

        _floatingHidden = !visible;

        // Уезжает вниз за край на свою высоту с отступом, а не гаснет прозрачностью:
        // прозрачная кнопка ловила бы касания поверх строк
        double shift = visible ? 0 : floating.Height + floating.Margin.Bottom + floating.Margin.Top;

        floating.InputTransparent = !visible;
        Guarded.Run(() => floating.TranslateToAsync(0, shift, 200, visible ? Easing.CubicOut : Easing.CubicIn));
    }

    /// <summary>
    /// Касание строки открывает операцию, а при выделении — отмечает строку.
    /// </summary>
    private void OnRowTapped(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: FeedRowItem row } || _model is not { } model)
        {
            return;
        }

        if (model.IsSelecting)
        {
            model.ToggleSelection(row.Key);
        }
        else
        {
            Navigator.Go($"{Routes.Transaction}?key={row.Key}");
        }
    }

    /// <summary>
    /// Долгое нажатие включает выделение с этой строки; при выделении — отмечает, как касание.
    /// </summary>
    private void OnRowLongPressed(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: FeedRowItem row } || _model is not { } model)
        {
            return;
        }

        if (model.IsSelecting)
        {
            model.ToggleSelection(row.Key);
        }
        else
        {
            model.StartSelection(row.Key);
        }
    }

    /// <summary>
    /// Смахивание при выделении закрывается сразу: строку там отмечают касанием,
    /// и «Удалить» у одной строки рядом с «Удалить» выделенного путало бы, что удалится.
    /// </summary>
    private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e)
    {
        if (sender is not SwipeView swipe)
        {
            return;
        }

        if (_model is { IsSelecting: true })
        {
            swipe.Close(animated: false);

            return;
        }

        // Открытой держится одна строка: две «Удалить» разом путали бы, что
        // удалится, а прежняя ускользнула бы от закрытия при выделении
        if (_swiped is { } previous && previous != swipe)
        {
            previous.Close();
            _swiped = null;
        }
    }

    private void OnSwipeEnded(object? sender, SwipeEndedEventArgs e)
    {
        if (sender is not SwipeView swipe)
        {
            return;
        }

        if (e.IsOpen)
        {
            _swiped = swipe;
        }
        else if (_swiped == swipe)
        {
            _swiped = null;
        }
    }

    private void OnSwipeDelete(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: FeedRowItem row } && _model is { IsSelecting: false } model)
        {
            // Строка закрывается нажатием «Удалить» сама: держать её — значит держать
            // в памяти вид, который после удаления уйдёт из списка
            _swiped = null;
            Guarded.Run(() => DeleteOnceAsync(() => DeleteOneAsync(model, row.Key)));
        }
    }

    /// <summary>
    /// Удаляет выделенное — после подтверждения. Зовёт шапка выделения
    /// (<see cref="SelectionBar"/>): она стоит в заголовке страницы, а не в списке.
    /// </summary>
    public void DeleteSelected()
    {
        if (_model is { } model)
        {
            Guarded.Run(() => DeleteOnceAsync(() => DeleteSelectedAsync(model)));
        }
    }

    private async Task DeleteOnceAsync(Func<Task> delete)
    {
        if (_deleting)
        {
            return;
        }

        _deleting = true;

        try
        {
            await delete();
        }
        finally
        {
            _deleting = false;
        }
    }

    /// <summary>
    /// Подтверждение обязательно, как в форме: отмены и корзины нет, и диалог —
    /// единственная защита. Он называет последствие — каким станет баланс.
    /// </summary>
    private async Task DeleteOneAsync(FeedViewModel model, Guid key)
    {
        TransactionDeletion deletion = await model.DeletePromptAsync(key);

        // Операцию уже удалили — спрашивать не о чем
        if (deletion.Count > 0 && await ConfirmAsync(deletion))
        {
            await model.DeleteAsync(key);
        }
    }

    /// <summary>
    /// Удалять нечего — выделенное уже удалено, — значит, и спрашивать не о чем:
    /// выделение снимается целиком, с отметками. Перечитывания после пустого
    /// удаления не будет, и отметки иначе остались бы на строках.
    /// </summary>
    private async Task DeleteSelectedAsync(FeedViewModel model)
    {
        TransactionDeletion deletion = await model.DeleteSelectedPromptAsync();

        if (deletion.Count == 0)
        {
            model.EndSelection();

            return;
        }

        if (await ConfirmAsync(deletion))
        {
            await model.DeleteSelectedAsync();
        }
    }

    private async Task<bool> ConfirmAsync(TransactionDeletion deletion) =>
        FindPage() is { } page
        && await page.DisplayAlertAsync(deletion.Title, deletion.Message, UiTexts.CommonDelete, UiTexts.CommonCancel);

    private Page? FindPage()
    {
        Element? element = Parent;

        while (element is not null and not Page)
        {
            element = element.Parent;
        }

        return element as Page;
    }
}
