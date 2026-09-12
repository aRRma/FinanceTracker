namespace Finance.Domain;

/// <summary>Тип счёта. Влияет только на значок и подпись, на расчёты — нет.</summary>
public enum AccountType
{
    /// <summary>Наличные.</summary>
    Cash,

    /// <summary>Карта.</summary>
    Card
}
