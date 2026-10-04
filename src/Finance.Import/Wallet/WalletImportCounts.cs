namespace Finance.Import.Wallet;

/// <summary>
/// Сколько записей в файле переноса или сколько их записано.
/// </summary>
/// <param name="Accounts">Счета.</param>
/// <param name="Places">Места.</param>
/// <param name="Transactions">Операции.</param>
public sealed record WalletImportCounts(int Accounts, int Places, int Transactions)
{
    /// <summary>
    /// Считает записи файла.
    /// </summary>
    /// <param name="file">Файл переноса.</param>
    public static WalletImportCounts Of(WalletImportFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new(file.Accounts.Count, file.Places.Count, file.Transactions.Count);
    }
}
