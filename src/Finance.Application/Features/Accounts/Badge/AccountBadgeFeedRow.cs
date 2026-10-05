using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Строка общей ленты в предпросмотре: последняя операция счёта, как её покажет лента,
/// с жетоном счёта в подписи. Своя запись, а не строка ленты: слайсы друг на друга не
/// ссылаются, а предпросмотру хватает одной строки без выделения и смахивания.
/// </summary>
/// <param name="Title">Подкатегория; у перевода — «Перевод».</param>
/// <param name="Caption">Место или группа перед жетоном; пусто — подпись из одного счёта.</param>
/// <param name="Amount">Сумма со знаком, уже отформатированная.</param>
/// <param name="IsPositive">Сумма положительна.</param>
/// <param name="IsExpense">Расход — смысловым цветом.</param>
/// <param name="Icon">Значок подкатегории или перевода.</param>
/// <param name="Account">Знак счёта — меняется по каждому касанию на экране.</param>
public sealed record AccountBadgeFeedRow(
    string Title,
    string Caption,
    string Amount,
    bool IsPositive,
    bool IsExpense,
    string Icon,
    AccountMark Account)
{
    private const string TransferIcon = "swap";

    /// <summary>
    /// Второй счёт перевода; у дохода и расхода пусто.
    /// </summary>
    public AccountMark? OtherAccount { get; init; }

    /// <summary>
    /// Счёт стоит в переводе стороной зачисления — в подписи он после стрелки.
    /// </summary>
    public bool IsTarget { get; init; }

    /// <summary>
    /// Подпись начинается текстом — перед жетоном разделитель.
    /// </summary>
    public bool HasCaptionText => Caption.Length > 0;

    /// <summary>
    /// Первый счёт подписи: у перевода — счёт списания, как в общей ленте.
    /// </summary>
    public AccountMark CaptionAccount => IsTarget && OtherAccount is { } source ? source : Account;

    /// <summary>
    /// Счёт после стрелки — у перевода; у остальных пусто.
    /// </summary>
    public AccountMark? CaptionTargetAccount => OtherAccount is null ? null : IsTarget ? Account : OtherAccount;

    /// <summary>
    /// Собирает строку из последней операции счёта.
    /// </summary>
    /// <param name="item">Строка ленты счёта.</param>
    /// <param name="account">Знак счёта, как он выбран сейчас.</param>
    public static AccountBadgeFeedRow From(FeedItem item, AccountMark account)
    {
        ArgumentNullException.ThrowIfNull(item);

        bool transfer = item.Kind is TransactionKind.Transfer;

        // Как в ленте: группа с тем же названием, что у подкатегории, подпись не пополняет
        string? group = string.Equals(item.Group, item.Title, StringComparison.OrdinalIgnoreCase)
            ? null
            : item.Group;

        return new AccountBadgeFeedRow(
            transfer ? UiTexts.KindTransfer : item.Title,
            transfer ? string.Empty : item.Place ?? group ?? string.Empty,
            item.Amount.DisplaySigned,
            item.Amount.IsPositive,
            item.Kind is TransactionKind.Expense,
            transfer ? TransferIcon : item.Icon ?? string.Empty,
            account)
        {
            // Строка ленты счёта у перевода — со стороны этого счёта, а общая лента
            // показывает перевод от списания к зачислению: оба счёта, через стрелку.
            // Сумма остаётся суммой этого счёта — сумму второй стороны лента счёта не несёт
            OtherAccount = transfer ? item.OtherAccount : null,
            IsTarget = transfer && item.Amount.IsPositive
        };
    }
}
