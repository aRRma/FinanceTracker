using Finance.Application.Infrastructure;

namespace Finance.App.Controls;

/// <summary>
/// Подпись строки ленты из частей: текст — место или группа, затем жетон и название
/// счёта; у перевода в общей ленте — два счёта через стрелку. Текст не шире половины
/// строки, название режется с конца: счёт важнее места, а место — первым, что режется.
/// </summary>
/// <remarks>
/// Колонки задаются в коде, а не разметкой: у перевода два названия делят строку
/// пополам, у остальных одно забирает всё, а в ленте счёта текст идёт во всю ширину.
/// Ячейки ленты переиспользуются, поэтому части не пересоздаются, а перенастраиваются
/// на каждую смену счёта.
/// </remarks>
public sealed class FeedCaption : Grid
{
    /// <summary>
    /// Текст подписи перед счётом.
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(FeedCaption),
        string.Empty,
        propertyChanged: static (bindable, _, _) => ((FeedCaption)bindable).Compose());

    /// <summary>
    /// Счёт в подписи; пусто — подпись из одного текста.
    /// </summary>
    public static readonly BindableProperty AccountProperty = BindableProperty.Create(
        nameof(Account),
        typeof(AccountMark),
        typeof(FeedCaption),
        propertyChanged: static (bindable, _, _) => ((FeedCaption)bindable).Compose());

    /// <summary>
    /// Счёт зачисления перевода; пусто у остальных.
    /// </summary>
    public static readonly BindableProperty TargetAccountProperty = BindableProperty.Create(
        nameof(TargetAccount),
        typeof(AccountMark),
        typeof(FeedCaption),
        propertyChanged: static (bindable, _, _) => ((FeedCaption)bindable).Compose());

    private const string Separator = " · ";
    private const string Arrow = " → ";

    // Жетон с отступом: половина строки у перевода — это жетон и название вместе
    private const double TransferSourceGap = 23;

    private readonly Label _text;
    private readonly Label _separator;
    private readonly AccountBadge _sourceToken;
    private readonly Label _sourceName;
    private readonly Label _arrow;
    private readonly AccountBadge _targetToken;
    private readonly Label _targetName;

    /// <summary>
    /// Создаёт подпись; текст и счета задаются привязкой.
    /// </summary>
    public FeedCaption()
    {
        _text = Part();
        _separator = Part(Separator);
        _sourceToken = Token();
        _sourceName = Part();
        _arrow = Part(Arrow);
        _targetToken = Token();
        _targetName = Part();

        ColumnSpacing = 0;
        ColumnDefinitions = [.. Enumerable.Range(0, 7).Select(static _ => new ColumnDefinition(GridLength.Auto))];

        View[] parts = [_text, _separator, _sourceToken, _sourceName, _arrow, _targetToken, _targetName];

        for (int column = 0; column < parts.Length; column++)
        {
            SetColumn((BindableObject)parts[column], column);
            Children.Add(parts[column]);
        }

        // Половина строки, а не число знаков: ширина знака зависит от шрифта системы
        SizeChanged += static (sender, _) => ((FeedCaption)sender!).Compose();

        Compose();
    }

    /// <summary>
    /// Текст подписи перед счётом.
    /// </summary>
    /// <remarks>
    /// Привязка через пустую ссылку («FeedRow.Caption», пока строки нет) кладёт сюда
    /// <see langword="null"/> — он читается пустой подписью.
    /// </remarks>
    public string Text
    {
        get => (string?)GetValue(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Счёт в подписи.
    /// </summary>
    public AccountMark? Account
    {
        get => (AccountMark?)GetValue(AccountProperty);
        set => SetValue(AccountProperty, value);
    }

    /// <summary>
    /// Счёт зачисления перевода.
    /// </summary>
    public AccountMark? TargetAccount
    {
        get => (AccountMark?)GetValue(TargetAccountProperty);
        set => SetValue(TargetAccountProperty, value);
    }

    private void Compose()
    {
        string text = Text;
        bool hasText = text.Length > 0;
        bool transfer = Account is not null && TargetAccount is not null;

        _text.Text = text;
        _text.IsVisible = hasText;
        _text.MaximumWidthRequest = Account is null || Width <= 0 ? double.PositiveInfinity : Width / 2;
        _separator.IsVisible = hasText && Account is not null;

        Show(_sourceToken, _sourceName, Account);
        _arrow.IsVisible = transfer;
        Show(_targetToken, _targetName, transfer ? TargetAccount : null);

        // Резиновая колонка одна — последняя видимая: без счёта это текст, у перевода —
        // счёт зачисления, у остальных — счёт. Счёт списания у перевода — по своей
        // ширине, но не шире половины строки: резиновым он отодвигал бы стрелку к середине
        ColumnDefinitions[0].Width = Account is null ? GridLength.Star : GridLength.Auto;
        ColumnDefinitions[3].Width = Account is null || transfer ? GridLength.Auto : GridLength.Star;
        ColumnDefinitions[6].Width = transfer ? GridLength.Star : GridLength.Auto;
        _sourceName.MaximumWidthRequest = transfer && Width > 0 ? Width / 2 - TransferSourceGap : double.PositiveInfinity;
    }

    private static void Show(AccountBadge token, Label name, AccountMark? account)
    {
        token.IsVisible = account is not null;
        name.IsVisible = account is not null;

        if (account is not null)
        {
            token.Tone = account.Color;
            token.IconKey = account.Icon;
            name.Text = account.Name;
        }
    }

    private static Label Part(string text = "")
    {
        Label label = new() { Text = text, LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center };

        if (ControlsApplication.Current?.Resources.TryGetValue("SecondaryLabel", out object? style) is true)
        {
            label.Style = (Style)style;
        }

        return label;
    }

    private static AccountBadge Token() =>
        new() { Mode = AccountBadgeMode.Token, Margin = new Thickness(0, 0, 4, 0) };
}
