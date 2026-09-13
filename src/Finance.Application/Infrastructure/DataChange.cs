namespace Finance.Application.Infrastructure;

/// <summary>
/// Что изменилось после команды. Экраны перечитывают себя по виду изменения,
/// а не по каждой записи: список счетов одинаково устаревает и от новой операции,
/// и от правки начального остатка.
/// </summary>
[Flags]
public enum DataChange
{
    /// <summary>Ничего не изменилось.</summary>
    None = 0,

    /// <summary>Счета: состав, порядок, признаки, начальный остаток.</summary>
    Accounts = 1,

    /// <summary>Категории обоих уровней.</summary>
    Categories = 2,

    /// <summary>Справочник мест.</summary>
    Places = 4,

    /// <summary>Операции, а вместе с ними балансы, лента и отчёт.</summary>
    Transactions = 8,

    /// <summary>Локальные настройки устройства.</summary>
    Settings = 16
}
