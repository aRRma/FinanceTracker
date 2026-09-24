namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Каким тоном форма показывает сумму крупно. Цвет по нему выбирает разметка:
/// модели цвета темы неизвестны. Незакрытое действие тоном не выражается:
/// итог тогда скрыт, подсказку показывает <c>HasAmountOperation</c>.
/// </summary>
public enum AmountTone
{
    /// <summary>
    /// Не задан. Настоящим значением не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Обычным текстом: перевод и зачисление.
    /// </summary>
    Plain = 1,

    /// <summary>
    /// Цветом расхода, со знаком минус.
    /// </summary>
    Expense = 2,

    /// <summary>
    /// Цветом дохода, со знаком плюс.
    /// </summary>
    Income = 3,

    /// <summary>
    /// Приглушённый ноль: сумма ещё не набрана или счёт не выбран.
    /// </summary>
    Placeholder = 4,
}
