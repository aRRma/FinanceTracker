namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Счёт в списке выбора: значок, название, баланс и отметка выбранного.
/// </summary>
/// <param name="Key">Ключ счёта.</param>
/// <param name="Icon">Ключ значка: наличные, карта или накопления.</param>
/// <param name="Name">Наименование.</param>
/// <param name="Caption">Подпись под названием: «Накопления» у скрытых из расчётов, иначе пусто.</param>
/// <param name="Balance">Баланс, уже отформатированный.</param>
/// <param name="IsNegative">Баланс отрицателен — показывается смысловым цветом.</param>
/// <param name="IsSelected">Этот счёт сейчас и стоит в форме.</param>
public sealed record AccountPickerRow(
    Guid Key,
    string Icon,
    string Name,
    string Caption,
    string Balance,
    bool IsNegative,
    bool IsSelected)
{
    /// <summary>
    /// Подпись под названием есть — строка двухстрочная.
    /// </summary>
    public bool HasCaption => Caption.Length > 0;
}
