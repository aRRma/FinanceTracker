using Finance.Application.Texts;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Feed;

/// <summary>
/// Строка ленты на экране: заголовок, подпись, заметка третьей строкой, сумма со знаком.
/// </summary>
/// <param name="Key">Ключ операции — по нему открывается карточка.</param>
/// <param name="Title">Подкатегория; у перевода — второй счёт в ленте счёта или «Перевод» в общей ленте.</param>
/// <param name="Caption">Подпись под заголовком: место или группа, в общей ленте ещё и счёт; у перевода в общей ленте — «откуда → куда».</param>
/// <param name="Note">Заметка или пусто — своей строкой под подписью.</param>
/// <param name="Amount">Сумма со знаком, уже отформатированная.</param>
/// <param name="IsPositive">Сумма положительна — доход или зачисление показывают смысловым цветом.</param>
/// <param name="IsExpense">
/// Расход — красится смысловым цветом. Списание перевода тоже отрицательно, но расходом
/// не считается: деньги не потрачены, а переложены, и красный на нём читался бы как трата.
/// </param>
/// <param name="Icon">Ключ значка: подкатегории или перевода.</param>
public sealed record FeedRowItem(
    Guid Key,
    string Title,
    string Caption,
    string Note,
    string Amount,
    bool IsPositive,
    bool IsExpense,
    string Icon)
{
    private const string TransferIcon = "swap";

    /// <summary>
    /// Заметка есть — под подписью её третья строка.
    /// </summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// Собирает строку экрана из строки ленты. Подпись — место, иначе группа, а в общей
    /// ленте ещё и счёт: вне ленты счёта он перестаёт быть очевидным из контекста.
    /// Заметка — своей строкой под подписью, а не вместо её части: в одной строке
    /// длинная заметка вытесняла группу и обрезала счёт в конце.
    /// </summary>
    /// <param name="item">Строка ленты из базы.</param>
    /// <param name="showAccount">Добавлять ли счёт в подпись — да в общей ленте.</param>
    public static FeedRowItem From(FeedItem item, bool showAccount)
    {
        ArgumentNullException.ThrowIfNull(item);

        string note = item.Note ?? string.Empty;

        // В общей ленте перевод виден со стороны списания, и его второй счёт
        // в заголовке без стрелки не говорит, куда ушли деньги: подпись
        // показывает направление целиком
        if (item.Kind is TransactionKind.Transfer && showAccount)
        {
            return new FeedRowItem(
                item.Key,
                UiTexts.KindTransfer,
                $"{item.AccountName} → {item.Title}",
                note,
                item.Amount.DisplaySigned,
                item.Amount.IsPositive,
                IsExpense: false,
                TransferIcon);
        }

        string? detail = item.Kind is TransactionKind.Transfer
            ? UiTexts.KindTransfer
            : item.Place ?? item.Group;

        string caption = (detail, showAccount) switch
        {
            (null, false) => string.Empty,
            (null, true) => item.AccountName,
            (_, false) => detail,
            (_, true) => $"{detail} · {item.AccountName}"
        };

        return new FeedRowItem(
            item.Key,
            item.Title,
            caption,
            note,
            item.Amount.DisplaySigned,
            item.Amount.IsPositive,
            item.Kind is TransactionKind.Expense,
            item.Kind is TransactionKind.Transfer ? TransferIcon : item.Icon ?? string.Empty);
    }
}
