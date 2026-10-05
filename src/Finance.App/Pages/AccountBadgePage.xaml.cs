using Finance.Application.Features.Accounts.Badge;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран «Цвет и значок» из карточки счёта. Касание меняет выбор сразу — экран
/// не закрывается: смотрят, как выглядит, и пробуют следующий.
/// </summary>
public sealed partial class AccountBadgePage : DataPage
{
    private readonly AccountBadgeViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана выбора.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AccountBadgePage(AccountBadgeViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Выбор живёт в общем объекте, а не в базе: перечитывание при появлении ничего
    /// бы не стёрло, но и нового не принесло.
    /// </remarks>
    protected override bool ReloadsOnAppearing => false;

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    // Данные ячейки — из BindingContext: sender приходит то распознавателем,
    // то самим элементом, а контекст у обоих один
    private void OnColorTapped(object? sender, TappedEventArgs e)
    {
        if (Choice<AccountColorChoice>(sender) is { } choice)
        {
            _model.PickColor(choice);
        }
    }

    private void OnIconTapped(object? sender, TappedEventArgs e)
    {
        if (Choice<AccountIconChoice>(sender) is { } choice)
        {
            _model.PickIcon(choice);
        }
    }

    private static T? Choice<T>(object? sender)
        where T : class =>
        sender switch
        {
            BindableObject { BindingContext: T choice } => choice,
            Element { Parent: BindableObject { BindingContext: T parent } } => parent,
            _ => null
        };
}
