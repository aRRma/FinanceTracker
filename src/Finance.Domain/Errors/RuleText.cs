namespace Finance.Domain.Errors;

/// <summary>
/// Каталог текстов о нарушенных правилах и подписей к ним. Ключ, а не сама
/// строка: текст лежит в ресурсах и переводится, а ключ остаётся прежним.
/// </summary>
/// <remarks>
/// Отдельное перечисление рядом с <see cref="Invariant"/>, а не его переиспользование:
/// одно правило объясняется разными словами в разных обстоятельствах — «у перевода
/// обязан быть счёт зачисления» и «счёт зачисления бывает только у перевода» нарушают
/// одно и то же правило с разных сторон.
/// </remarks>
public enum RuleText
{
    /// <summary>
    /// Значение неинициализированной переменной. Текстом не бывает.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Сумма записана точнее копейки.
    /// </summary>
    AmountInWholeKopecks = 1,

    /// <summary>
    /// Сумма больше предела, заданного домену.
    /// </summary>
    AmountWithinLimit = 2,

    /// <summary>
    /// Действие над суммами в разных валютах.
    /// </summary>
    CurrenciesNeverMixed = 3,

    /// <summary>
    /// Сумма операции не больше нуля.
    /// </summary>
    AmountIsPositive = 4,

    /// <summary>
    /// У перевода не указан счёт зачисления.
    /// </summary>
    TransferNeedsTargetAccount = 5,

    /// <summary>
    /// Счёт зачисления указан не у перевода.
    /// </summary>
    TargetAccountOnlyInTransfer = 6,

    /// <summary>
    /// У перевода не указана сумма зачисления.
    /// </summary>
    TransferNeedsTargetAmount = 7,

    /// <summary>
    /// Сумма зачисления указана не у перевода.
    /// </summary>
    TargetAmountOnlyInTransfer = 8,

    /// <summary>
    /// Перевод со счёта на него же.
    /// </summary>
    TransferAccountsDiffer = 9,

    /// <summary>
    /// Суммы перевода внутри одной валюты разошлись.
    /// </summary>
    SameCurrencyTransferAmountsEqual = 10,

    /// <summary>
    /// У перевода указана категория.
    /// </summary>
    CategoryNotInTransfer = 11,

    /// <summary>
    /// У дохода или расхода нет категории.
    /// </summary>
    CategoryRequiredInIncomeAndExpense = 12,

    /// <summary>
    /// У перевода указано место.
    /// </summary>
    PlaceNotInTransfer = 13,

    /// <summary>
    /// Заметка длиннее предела.
    /// </summary>
    NoteWithinLimit = 14,

    /// <summary>
    /// Дата раньше начала учёта.
    /// </summary>
    DateTooEarly = 15,

    /// <summary>
    /// Дата в будущем.
    /// </summary>
    DateInFuture = 16,

    /// <summary>
    /// Имя пусто или состоит из пробелов.
    /// </summary>
    NameEmpty = 17,

    /// <summary>
    /// Имя уже занято соседом по области поиска.
    /// </summary>
    NameTaken = 18,

    /// <summary>
    /// Валюту счёта меняют после первой операции.
    /// </summary>
    CurrencyFixedOnceUsed = 19,

    /// <summary>
    /// Дату открытия счёта сдвигают за его же операцию.
    /// </summary>
    OpenedOnNotAfterTransactions = 20,

    /// <summary>
    /// Роль приёмника «Прочее» отдают группе.
    /// </summary>
    ReceiverIsNotGroup = 21,

    /// <summary>
    /// Удаляют приёмник группы или служебную подкатегорию.
    /// </summary>
    ProtectedCategoryNotDeleted = 22,

    /// <summary>
    /// Удаляют группу.
    /// </summary>
    GroupNotDeleted = 23,

    /// <summary>
    /// Переносят приёмник группы или служебную подкатегорию.
    /// </summary>
    ProtectedCategoryNotMoved = 24,

    /// <summary>
    /// Переносят группу: уровней всего два.
    /// </summary>
    GroupNotMoved = 25,

    /// <summary>
    /// Переносят подкатегорию в служебную группу.
    /// </summary>
    ServiceGroupClosedToMoves = 26,

    /// <summary>
    /// Переносят подкатегорию в группу другого вида.
    /// </summary>
    MoveKeepsKind = 27,

    /// <summary>
    /// У служебной группы не одна подкатегория или есть приёмник.
    /// </summary>
    ServiceGroupHasOneSubcategory = 28,

    /// <summary>
    /// У обычной группы приёмник не один.
    /// </summary>
    GroupHasOneReceiver = 29,

    /// <summary>
    /// В служебной группе ищут приёмник, которого ей не положено.
    /// </summary>
    ServiceGroupHasNoReceiver = 30,

    /// <summary>
    /// Заблокированный счёт указан в новой операции.
    /// </summary>
    ClosedAccountNotInNewTransaction = 31,

    /// <summary>
    /// Операция датирована раньше открытия своего счёта.
    /// </summary>
    TransactionNotBeforeAccountOpened = 32,

    /// <summary>
    /// В операции указана группа вместо подкатегории.
    /// </summary>
    CategoryIsSubcategory = 33,

    /// <summary>
    /// Вид операции не совпадает с видом группы её категории.
    /// </summary>
    CategoryKindMatchesTransaction = 34,

    /// <summary>
    /// Подпись счёта в сообщении о его имени.
    /// </summary>
    SubjectAccount = 35,

    /// <summary>
    /// Подпись группы категорий.
    /// </summary>
    SubjectGroup = 36,

    /// <summary>
    /// Подпись подкатегории.
    /// </summary>
    SubjectSubcategory = 37,

    /// <summary>
    /// Подпись категории без уточнения уровня — при переименовании.
    /// </summary>
    SubjectCategory = 38,

    /// <summary>
    /// Подпись места.
    /// </summary>
    SubjectPlace = 39,

    /// <summary>
    /// Подпись группы расходов: имя уникально внутри своего вида.
    /// </summary>
    SubjectExpenseGroup = 40,

    /// <summary>
    /// Подпись группы доходов.
    /// </summary>
    SubjectIncomeGroup = 41,

    /// <summary>
    /// Подпись подкатегории с названием её группы: имя уникально внутри группы.
    /// </summary>
    SubjectSubcategoryOfGroup = 42,

    /// <summary>
    /// Подпись даты операции.
    /// </summary>
    SubjectTransactionDate = 43,

    /// <summary>
    /// Подпись даты открытия счёта.
    /// </summary>
    SubjectAccountOpenedOn = 44,

    /// <summary>
    /// Подпись суммы операции.
    /// </summary>
    SubjectTransactionAmount = 45,

    /// <summary>
    /// Подпись суммы зачисления перевода.
    /// </summary>
    SubjectTargetAmount = 46,

    /// <summary>
    /// Подпись начального остатка счёта.
    /// </summary>
    SubjectOpeningBalance = 47,

    /// <summary>
    /// Действие сложения — в сообщении о разных валютах.
    /// </summary>
    ActionAdd = 48,

    /// <summary>
    /// Действие вычитания — в сообщении о разных валютах.
    /// </summary>
    ActionSubtract = 49,

    /// <summary>
    /// Действие сравнения — в сообщении о разных валютах.
    /// </summary>
    ActionCompare = 50
}
