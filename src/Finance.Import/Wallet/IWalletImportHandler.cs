namespace Finance.Import.Wallet;

/// <summary>
/// Разовый перенос истории из Wallet в пустую базу.
/// </summary>
public interface IWalletImportHandler
{
    /// <summary>
    /// Можно ли переносить: в базе нет ни счетов, ни мест, ни операций, в том числе удалённых.
    /// Стартовый набор категорий переносу не мешает — перенос на него и рассчитан.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><see langword="true"/>, если база пуста.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает счета, места и операции файла одной транзакцией: нарушенное
    /// правило на любой записи откатывает перенос целиком, и база остаётся пустой.
    /// </summary>
    /// <param name="file">Файл переноса.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Сколько записано.</returns>
    Task<WalletImportCounts> HandleAsync(WalletImportFile file, CancellationToken cancellationToken = default);
}
