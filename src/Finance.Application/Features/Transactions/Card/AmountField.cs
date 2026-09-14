namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Какое из полей суммы набирается сейчас. Клавиатура в форме одна, а полей
/// у перевода между валютами два — без явного признака нажатия уходили бы
/// всегда в первое.
/// </summary>
public enum AmountField
{
    /// <summary>Сумма списания. Ею набор и начинается.</summary>
    Source,

    /// <summary>Сумма зачисления — только у перевода между разными валютами.</summary>
    Target
}
