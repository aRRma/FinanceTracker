using Finance.Domain.Enums;

namespace Finance.Import.Wallet;

/// <summary>
/// Счёт в файле переноса. Порядок счетов в файле — порядок на главном экране.
/// </summary>
/// <param name="Name">Наименование. По нему на счёт ссылаются операции файла.</param>
/// <param name="Type">Наличные или карта.</param>
/// <param name="Currency">Валюта счёта.</param>
/// <param name="OpeningBalance">Начальный остаток.</param>
/// <param name="OpenedOn">Дата открытия — не позже первой операции счёта.</param>
/// <param name="ExcludedFromTotals">«Скрытый».</param>
public sealed record WalletImportAccount(
    string Name,
    AccountType Type,
    Currency Currency,
    decimal OpeningBalance,
    DateOnly OpenedOn,
    bool ExcludedFromTotals);
