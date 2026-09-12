namespace Finance.Domain;

/// <summary>
/// Вид категории. Отдельное перечисление от <see cref="TransactionKind"/> намеренно:
/// у группы не бывает вида «перевод», и общий тип позволил бы его записать.
/// </summary>
public enum CategoryKind
{
    /// <summary>Расход.</summary>
    Expense,

    /// <summary>Доход.</summary>
    Income
}
