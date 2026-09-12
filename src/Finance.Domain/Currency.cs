namespace Finance.Domain;

/// <summary>Валюта счёта. Конвертация между валютами не выполняется никогда.</summary>
public enum Currency
{
    /// <summary>Рубль. Валюта итогов дня и отчёта — единственная, в которой считаются общие суммы.</summary>
    RUB,

    /// <summary>Доллар США.</summary>
    USD,

    /// <summary>Евро.</summary>
    EUR
}
