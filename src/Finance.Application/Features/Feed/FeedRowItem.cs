using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Feed;

/// <summary>
/// Строка ленты на экране: заголовок, подпись, сумма со знаком.
/// </summary>
/// <param name="Key">Ключ операции — по нему открывается карточка.</param>
/// <param name="Title">Подкатегория; у перевода — второй счёт в ленте счёта, заметка или «Перевод» в общей ленте.</param>
/// <param name="Caption">Подпись под заголовком: заметка, место или группа, в общей ленте ещё и счёт; у перевода в общей ленте — «откуда → куда».</param>
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
    string Amount,
    bool IsPositive,
    bool IsExpense,
    string Icon)
{
    private const string TransferIcon = "swap";

    /// <summary>
    /// Собирает строку экрана из строки ленты. Подписью служит заметка, если она есть,
    /// иначе место, иначе группа: заметка — единственное, чего не вывести из названия
    /// подкатегории. В общей ленте к подписи добавляется счёт — вне ленты счёта
    /// он перестаёт быть очевидным из контекста.
    /// </summary>
    /// <param name="item">Строка ленты из базы.</param>
    /// <param name="showAccount">Добавлять ли счёт в подпись — да в общей ленте.</param>
    public static FeedRowItem From(FeedItem item, bool showAccount)
    {
        ArgumentNullException.ThrowIfNull(item);

        // В общей ленте перевод виден со стороны списания, и его второй счёт
        // в заголовке без стрелки не говорит, куда ушли деньги: заголовком идёт
        // заметка, а подпись показывает направление целиком
        if (item.Kind is TransactionKind.Transfer && showAccount)
        {
            return new FeedRowItem(
                item.Key,
                item.Note ?? "Перевод",
                $"{item.AccountName} → {item.Title}",
                item.Amount.DisplaySigned,
                item.Amount.IsPositive,
                IsExpense: false,
                TransferIcon);
        }

        string? detail = item.Kind is TransactionKind.Transfer
            ? item.Note ?? "Перевод"
            : item.Note ?? item.Place ?? item.Group;

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
            item.Amount.DisplaySigned,
            item.Amount.IsPositive,
            item.Kind is TransactionKind.Expense,
            item.Kind is TransactionKind.Transfer ? TransferIcon : item.Icon ?? string.Empty);
    }
}
