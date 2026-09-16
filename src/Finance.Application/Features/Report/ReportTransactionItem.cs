using Finance.Application.Infrastructure;

namespace Finance.Application.Features.Report;

/// <summary>
/// Строка третьего уровня: место, дата со счётом, сумма со знаком.
/// </summary>
/// <param name="Key">Ключ операции — по нему открывается карточка.</param>
/// <param name="Title">Место; без места — заметка; без той и другой — подкатегория.</param>
/// <param name="Caption">Подпись: «28 августа · Карта основная».</param>
/// <param name="Amount">Сумма со знаком, уже отформатированная.</param>
/// <param name="IsPositive">Сумма положительна — доход показывают смысловым цветом.</param>
public sealed record ReportTransactionItem(Guid Key, string Title, string Caption, string Amount, bool IsPositive)
{
    /// <summary>
    /// Расход — красится смысловым цветом. Отдельного признака вида не нужно:
    /// переводы в отчёт не входят вовсе, и отрицательная сумма здесь — всегда трата.
    /// </summary>
    public bool IsExpense => !IsPositive;

    /// <summary>
    /// Собирает строку. Заголовком служит место: подкатегория уже стоит в шапке
    /// экрана, и повторять её в каждой строке значит не сказать ничего. Год в дате
    /// не пишется — месяц стоит в той же шапке.
    /// </summary>
    /// <param name="item">Операция из базы.</param>
    /// <param name="subcategory">Название подкатегории — заголовок на крайний случай.</param>
    public static ReportTransactionItem From(ReportTransaction item, string subcategory)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ReportTransactionItem(
            item.Key,
            item.Place ?? item.Note ?? subcategory,
            $"{DateText.Day(item.OccurredOn)} · {item.AccountName}",
            item.Amount.DisplaySigned,
            item.Amount.IsPositive);
    }
}
