namespace Finance.Domain.Enums;

/// <summary>
/// Вид категории. Отдельное перечисление от <see cref="TransactionKind"/> намеренно:
/// у группы не бывает вида «перевод», и общий тип позволил бы его записать.
/// </summary>
public enum CategoryKind
{
    /// <summary>
    /// Значение неинициализированной переменной. Настоящим видом категории не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Расход.
    /// </summary>
    Expense = 1,

    /// <summary>
    /// Доход.
    /// </summary>
    Income = 2
}
