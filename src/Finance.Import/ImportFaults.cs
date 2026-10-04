namespace Finance.Import;

/// <summary>
/// Тексты отказов переноса. Их читает тот, кто готовит перенос на рабочей машине,
/// а не пользователь приложения: файл с ошибкой до телефона не доходит.
/// </summary>
/// <remarks>
/// Намеренно мимо ресурсов текста, как и <c>Faults</c> прикладного слоя: переводить
/// их некому, а собранные в одном месте они видны все разом.
/// </remarks>
internal static class ImportFaults
{
    /// <summary>
    /// Файл переноса разобрался в пустоту.
    /// </summary>
    internal static string WalletImportFileEmpty() => "Файл переноса из Wallet пуст";

    /// <summary>
    /// Файл переноса чужого формата или другой версии.
    /// </summary>
    /// <param name="format">Формат, записанный в файле.</param>
    /// <param name="version">Версия, записанная в файле.</param>
    internal static string WalletImportFormat(string format, int version) =>
        $"Файл формата «{format}» версии {version} не является файлом переноса из Wallet этой версии";

    /// <summary>
    /// Элемент списка файла переноса пуст или испорчен.
    /// </summary>
    /// <param name="list">Какой список.</param>
    /// <param name="index">Номер элемента, с нуля.</param>
    internal static string WalletImportEntry(string list, int index) =>
        $"Файл переноса из Wallet испорчен: {list}[{index}] пуст, с пробелами по краям имени, с неизвестным значением или с моментом не в UTC";

    /// <summary>
    /// Перенос запущен не на пустой базе.
    /// </summary>
    internal static string WalletImportNotEmpty() =>
        "Перенос из Wallet идёт только в пустую базу, а в ней уже есть счета, места или операции";

    /// <summary>
    /// Операция файла ссылается на счёт, которого в файле нет.
    /// </summary>
    /// <param name="index">Номер операции в файле, с нуля.</param>
    /// <param name="name">Имя счёта.</param>
    internal static string WalletImportUnknownAccount(int index, string name) =>
        $"Операция {index} файла переноса ссылается на счёт «{name}», которого в файле нет";

    /// <summary>
    /// Операция файла ссылается на место, которого в файле нет.
    /// </summary>
    /// <param name="index">Номер операции в файле, с нуля.</param>
    /// <param name="name">Имя места.</param>
    internal static string WalletImportUnknownPlace(int index, string name) =>
        $"Операция {index} файла переноса ссылается на место «{name}», которого в файле нет";

    /// <summary>
    /// Операция файла ссылается на подкатегорию, которой нет в базе.
    /// </summary>
    /// <param name="index">Номер операции в файле, с нуля.</param>
    /// <param name="key">Текстовый ключ подкатегории стартового набора.</param>
    internal static string WalletImportUnknownCategory(int index, string key) =>
        $"Операция {index} файла переноса ссылается на подкатегорию «{key}», которой нет в базе";

    /// <summary>
    /// Перевод файла между счетами разных валют: сумма в файле одна, пересчитать её не из чего.
    /// </summary>
    /// <param name="index">Номер операции в файле, с нуля.</param>
    internal static string WalletImportTransferCurrencies(int index) =>
        $"Операция {index} файла переноса — перевод между счетами разных валют";
}
