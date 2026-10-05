using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Экран «Цвет и значок» из карточки счёта. Сверху — предпросмотр: строка балансов
/// и строка общей ленты, обе меняются по каждому касанию. Ниже — восемь цветов и сетка
/// значков с подсказкой по названию. Выбор уходит в карточку, а в базу — только по
/// «Сохранить» в карточке: экран — часть формы, а не отдельная правка.
/// </summary>
public sealed partial class AccountBadgeViewModel : ObservableObject
{
    private readonly AccountBadgeDraft _draft;
    private readonly IFeedQuery _feed;

    /// <summary>
    /// Создаёт модель представления экрана выбора.
    /// </summary>
    /// <param name="draft">Счёт, как он набран в карточке, и куда кладётся выбор.</param>
    /// <param name="feed">Лента счёта — последняя операция для предпросмотра.</param>
    public AccountBadgeViewModel(AccountBadgeDraft draft, IFeedQuery feed)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(feed);

        _draft = draft;
        _feed = feed;
    }

    /// <summary>
    /// Знак счёта, как он выглядит сейчас.
    /// </summary>
    [ObservableProperty]
    public partial AccountMark Mark { get; private set; } = new(string.Empty, AccountColor.Blue, AccountIcon.ByType(AccountType.Card, excludedFromTotals: false));

    /// <summary>
    /// Баланс в строке предпросмотра.
    /// </summary>
    public string Balance => _draft.Balance;

    /// <summary>
    /// Баланс отрицателен — смысловым цветом, как на балансах.
    /// </summary>
    public bool IsNegative => _draft.IsNegative;

    /// <summary>
    /// Цвета в порядке очереди с отметкой выбранного.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<AccountColorChoice> Colors { get; private set; } = [];

    /// <summary>
    /// Значки в порядке сетки с отметкой того, которым счёт рисуется.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<AccountIconChoice> Icons { get; private set; } = [];

    /// <summary>
    /// Значки, подходящие к названию, — над сеткой; пусто — подсказки нет.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHints))]
    public partial IReadOnlyList<AccountIconChoice> Hints { get; private set; } = [];

    /// <summary>
    /// К названию есть подсказка.
    /// </summary>
    public bool HasHints => Hints.Count > 0;

    /// <summary>
    /// Последняя операция счёта — строка ленты в предпросмотре; пусто у нового счёта
    /// и у счёта без операций.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeedPreview))]
    public partial AccountBadgeFeedRow? FeedRow { get; private set; }

    /// <summary>
    /// Есть что показать строкой ленты.
    /// </summary>
    public bool HasFeedPreview => FeedRow is not null;

    /// <summary>
    /// Читает счёт из карточки и его последнюю операцию.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Refresh();
        OnPropertyChanged(nameof(Balance));
        OnPropertyChanged(nameof(IsNegative));

        if (_draft.Key is not { } key)
        {
            return;
        }

        // ConfigureAwait(false) здесь недопустим: следом правится привязанное свойство
        FeedPage page = await _feed.ReadAsync(key, after: null, take: 1, cancellationToken);

        FeedRow = page.Items is [var last, ..] ? AccountBadgeFeedRow.From(last, Mark) : null;
    }

    /// <summary>
    /// Касание цвета.
    /// </summary>
    /// <param name="choice">Выбранная ячейка.</param>
    public void PickColor(AccountColorChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        _draft.Color = choice.Color;
        _draft.IsChanged = true;
        Refresh();
    }

    /// <summary>
    /// Касание значка — в сетке или в подсказке.
    /// </summary>
    /// <param name="choice">Выбранная ячейка.</param>
    public void PickIcon(AccountIconChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        _draft.Icon = choice.Key;
        _draft.IsChanged = true;
        Refresh();
    }

    /// <summary>
    /// Пересобирает знак и отметки после каждого касания: ячейки — записи, и
    /// отметка меняется заменой списка, а не оповещением из ячейки.
    /// </summary>
    private void Refresh()
    {
        string icon = AccountIcon.For(_draft.Type, _draft.ExcludedFromTotals, _draft.Icon);

        Mark = new AccountMark(_draft.Name, _draft.Color, icon);
        Colors = [.. AccountColors.Queue.Select(color => new AccountColorChoice(color, AccountBadgeText.ColorName(color), color == _draft.Color))];
        Icons = [.. AccountIcon.Choices.Select(key => Choice(key, icon))];
        Hints = [.. AccountIconHints.For(_draft.Name).Select(key => Choice(key, icon))];
        FeedRow = FeedRow is { } row ? row with { Account = Mark } : null;
    }

    private AccountIconChoice Choice(string key, string current) =>
        new(key, IconNames.Of(key), key == current, _draft.Color);
}
