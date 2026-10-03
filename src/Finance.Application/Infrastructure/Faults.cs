using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Тексты сбоев, которые пишутся не пользователю, а тому, кто читает журнал:
/// запись пропала между чтением и записью, ресурс не вшит в сборку, стартовый
/// набор разошёлся сам с собой.
/// </summary>
/// <remarks>
/// Намеренно мимо ресурсов текста: эти строки не показываются на экране
/// (<c>Guarded</c> выпускает наружу только доменные правила и сорванную миграцию),
/// переводить их некому и незачем, а собранные в одном месте они видны все разом.
/// </remarks>
internal static class Faults
{
    /// <summary>
    /// Счёт, на который ссылается операция или команда, пропал из базы.
    /// </summary>
    /// <param name="key">Ключ счёта.</param>
    internal static string AccountNotFound(Guid key) => $"Счёт {key} не найден";

    /// <summary>
    /// Команда заводит счёт сразу заблокированным: блокируют только заведённый.
    /// </summary>
    internal static string NewAccountClosed() => "Новый счёт не заводится заблокированным — блокируют только заведённый";

    /// <summary>
    /// Место пропало из базы между чтением и переименованием.
    /// </summary>
    /// <param name="key">Ключ места.</param>
    internal static string PlaceNotFound(Guid key) => $"Место {key} не найдено";

    /// <summary>
    /// Категория пропала из базы.
    /// </summary>
    /// <param name="key">Ключ категории.</param>
    internal static string CategoryNotFound(Guid key) => $"Категория {key} не найдена";

    /// <summary>
    /// Группа пропала из базы.
    /// </summary>
    /// <param name="key">Ключ группы.</param>
    internal static string GroupNotFound(Guid key) => $"Группа {key} не найдена";

    /// <summary>
    /// Группа подкатегории пропала из базы.
    /// </summary>
    /// <param name="parentKey">Ключ группы.</param>
    /// <param name="subcategoryKey">Ключ подкатегории.</param>
    internal static string GroupOfSubcategoryNotFound(Guid parentKey, Guid subcategoryKey) =>
        $"Группа {parentKey} подкатегории {subcategoryKey} не найдена";

    /// <summary>
    /// Группа удаляемой подкатегории пропала из базы.
    /// </summary>
    /// <param name="parentKey">Ключ группы.</param>
    /// <param name="name">Название удаляемой подкатегории.</param>
    internal static string GroupOfDeletedNotFound(Guid parentKey, string name) =>
        $"Группа {parentKey} удаляемой категории «{name}» не найдена";

    /// <summary>
    /// Операция пропала из базы между чтением и правкой.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    internal static string TransactionNotFound(Guid key) => $"Операция {key} не найдена";

    /// <summary>
    /// У группы в базе нет вида, хотя он обязателен: колонка общая с подкатегориями,
    /// и пустой вид у группы означает испорченную строку.
    /// </summary>
    /// <param name="name">Название группы.</param>
    internal static string GroupKindMissing(string name) => $"У группы «{name}» не задан вид";

    /// <summary>
    /// Команда завести группу пришла без вида.
    /// </summary>
    internal static string GroupKindRequired() => "Вид заводимой группы обязателен";

    /// <summary>
    /// Валютный перевод пришёл без суммы зачисления: курс задаёт пользователь,
    /// и вывести её приложению не из чего.
    /// </summary>
    /// <param name="source">Валюта счёта списания.</param>
    /// <param name="target">Валюта счёта зачисления.</param>
    internal static string TransferAmountMissing(Currency source, Currency target) =>
        $"Перевод из {source} в {target} без суммы зачисления";

    /// <summary>
    /// Часть категорий справочника не отнеслась ни к одной группе.
    /// </summary>
    /// <param name="rows">Сколько категорий прочитано.</param>
    /// <param name="items">Сколько разложено по группам.</param>
    internal static string CategoriesLost(int rows, int items) =>
        $"В справочнике {rows} категорий, а к группам отнесены {items}: есть подкатегория без группы";

    /// <summary>
    /// Встроенный в сборку ресурс не найден: сборка собрана без него.
    /// </summary>
    /// <param name="name">Имя ресурса.</param>
    internal static string ResourceMissing(string name) => $"Ресурс {name} не вшит в сборку";

    /// <summary>
    /// Набор значков разобрался в пустоту.
    /// </summary>
    internal static string IconSetEmpty() => "Набор значков пуст";

    /// <summary>
    /// Стартовый набор разобрался в пустоту.
    /// </summary>
    internal static string PresetEmpty() => "Стартовый набор пуст";

    /// <summary>
    /// Идентификатор категории в стартовом наборе не выводится из её ключа.
    /// </summary>
    /// <param name="key">Ключ категории.</param>
    /// <param name="stored">Идентификатор, записанный в наборе.</param>
    /// <param name="derived">Идентификатор, выведенный из ключа.</param>
    internal static string PresetIdMismatch(string key, Guid stored, Guid derived) =>
        $"Идентификатор категории «{key}» в стартовом наборе — {stored}, а выводится {derived}";

    /// <summary>
    /// Путь к файлу базы не задан.
    /// </summary>
    internal static string DatabasePathMissing() => "Путь к базе не задан";

    /// <summary>
    /// Сумма точнее копейки дошла до записи в базу: домен такую не создаёт,
    /// значит запись собрана в обход него.
    /// </summary>
    /// <param name="amount">Сумма.</param>
    internal static string AmountTooPrecise(decimal amount) =>
        $"Сумма {amount} точнее копейки и в базу не записывается";

    /// <summary>
    /// У строки перевода есть сумма зачисления, а валюта второго счёта не передана:
    /// обнулить её молча нельзя, перевод потерял бы вторую сторону.
    /// </summary>
    /// <param name="key">Ключ операции.</param>
    internal static string TargetCurrencyMissing(Guid key) =>
        $"У операции {key} есть сумма зачисления, а валюта счёта зачисления не передана";

    /// <summary>
    /// Выбранная зона системе неизвестна: список читается один раз на заход,
    /// и за это время систему могли обновить.
    /// </summary>
    /// <param name="id">Идентификатор зоны.</param>
    internal static string TimeZoneUnknown(string id) => $"Зона «{id}» системе неизвестна";

    /// <summary>
    /// Клавиша клавиатуры суммы названа не одним знаком.
    /// </summary>
    internal static string KeypadKeyIsOneSign() => "Клавиша суммы называется одним знаком";

    /// <summary>
    /// Такой клавиши на клавиатуре суммы нет.
    /// </summary>
    internal static string KeypadKeyUnknown() => "Такой клавиши на клавиатуре суммы нет";

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
        $"Файл формата «{format}» версии {version} не является файлом переноса из Wallet этой версии приложения";

    /// <summary>
    /// Элемент списка файла переноса пуст или испорчен.
    /// </summary>
    /// <param name="list">Какой список.</param>
    /// <param name="index">Номер элемента, с нуля.</param>
    internal static string WalletImportEntry(string list, int index) =>
        $"Файл переноса из Wallet испорчен: {list}[{index}] пуст, с пробелами по краям имени, с неизвестным значением или с моментом не в UTC";

    /// <summary>
    /// Запись начата, пока идёт предыдущая.
    /// </summary>
    internal static string WalletImportRunning() => "Перенос из Wallet уже идёт";

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

    /// <summary>
    /// Восстановление запрошено без проверенного файла: проверку пропустили или файл не подошёл.
    /// </summary>
    internal static string RecoveryNotChecked() => "Восстанавливать нечего: файл не проверен или не подошёл";

    /// <summary>
    /// Выгрузка или восстановление запущены, пока идёт прежнее действие экрана «Данные».
    /// </summary>
    internal static string ExportRunning() => "Выгрузка или восстановление уже идут";

    /// <summary>
    /// Замену подтвердили раньше, чем кончился отсчёт предупреждения.
    /// </summary>
    internal static string RecoveryWarningRunning() => "Замена подтверждена до конца отсчёта предупреждения";
}
