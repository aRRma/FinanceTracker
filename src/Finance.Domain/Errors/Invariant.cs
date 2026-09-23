using Finance.Domain.Entities;
using Finance.Domain.Rules;
using Finance.Domain.Values;

namespace Finance.Domain.Errors;

/// <summary>
/// Правила, которые доменные типы соблюдают всегда. Имя члена говорит само за себя,
/// чтобы тест и сообщение об ошибке читались без справочника требований.
/// </summary>
/// <remarks>
/// Часть правил проверяется исключением, часть соблюдается самим устройством типа —
/// у них нет <see cref="DomainException"/>, но есть тест. Номера проставлены явно и
/// нигде не хранятся: они держат значение члена на месте при перестановке строк.
/// </remarks>
public enum Invariant
{
    /// <summary>
    /// Значение неинициализированной переменной. Правилом не является: ни в требованиях,
    /// ни в тестах ему места нет, и сверка покрытия его пропускает.
    /// </summary>
    Unknown = 0,

    // Суммы

    /// <summary>
    /// Сумма всегда положительна, знак в балансе задаёт вид операции.
    /// </summary>
    AmountSignComesFromKind = 1,

    /// <summary>
    /// Сумма операции строго больше нуля.
    /// </summary>
    AmountIsPositive = 2,

    /// <summary>
    /// Сумма по модулю не превышает <see cref="Money.Limit"/>.
    /// </summary>
    AmountWithinLimit = 3,

    /// <summary>
    /// Сумма выражена в целых копейках: не больше двух знаков после запятой.
    /// </summary>
    AmountInWholeKopecks = 4,

    /// <summary>
    /// Суммы в разных валютах не складываются, не вычитаются и не сравниваются.
    /// </summary>
    CurrenciesNeverMixed = 5,

    // Операция

    /// <summary>
    /// Счёт и сумма зачисления есть у перевода и только у него.
    /// </summary>
    TargetOnlyInTransfer = 6,

    /// <summary>
    /// Перевод идёт между разными счетами.
    /// </summary>
    TransferAccountsDiffer = 7,

    /// <summary>
    /// Перевод внутри одной валюты списывает и зачисляет одну и ту же сумму.
    /// </summary>
    SameCurrencyTransferAmountsEqual = 8,

    /// <summary>
    /// Категория обязательна для дохода и расхода и запрещена для перевода.
    /// </summary>
    CategoryOnlyInIncomeAndExpense = 9,

    /// <summary>
    /// Операция относится к подкатегории, а не к группе.
    /// </summary>
    CategoryIsSubcategory = 10,

    /// <summary>
    /// Вид группы категории совпадает с видом операции — либо группа универсальна
    /// и принимает оба вида.
    /// </summary>
    CategoryKindMatchesTransaction = 11,

    /// <summary>
    /// Место необязательно для дохода и расхода и запрещено для перевода.
    /// </summary>
    PlaceNotInTransfer = 12,

    /// <summary>
    /// Дата операции не раньше открытия каждого её счёта.
    /// </summary>
    TransactionNotBeforeAccountOpened = 13,

    /// <summary>
    /// Дата операции — от <see cref="Dates.Earliest"/> до сегодняшнего дня включительно.
    /// </summary>
    TransactionDateInRange = 14,

    /// <summary>
    /// Заметка не длиннее <see cref="Transaction.MaxNoteLength"/>.
    /// </summary>
    NoteWithinLimit = 15,

    /// <summary>
    /// Закрытый счёт не попадает в новую операцию и не подставляется при правке.
    /// </summary>
    ClosedAccountNotInNewTransaction = 16,

    /// <summary>
    /// Правка операции заменяет её состояние целиком, включая вид.
    /// </summary>
    EditReplacesWholeState = 17,

    /// <summary>
    /// Смена вида сразу стирает поля, которых у нового вида не бывает.
    /// </summary>
    KindChangeClearsForbiddenFields = 18,

    // Счёт

    /// <summary>
    /// Валюта счёта не меняется, если по нему когда-либо была операция.
    /// </summary>
    CurrencyFixedOnceUsed = 19,

    /// <summary>
    /// Дату открытия нельзя сдвинуть позже самой ранней операции.
    /// </summary>
    OpenedOnNotAfterTransactions = 20,

    /// <summary>
    /// Дата открытия счёта — в тех же границах, что и дата операции.
    /// </summary>
    OpeningDateInRange = 21,

    // Категории

    /// <summary>
    /// Уровней категорий ровно два: группа и подкатегория.
    /// </summary>
    TwoCategoryLevels = 22,

    /// <summary>
    /// Подкатегория ссылается на группу, группа — ни на что.
    /// </summary>
    SubcategoryBelongsToGroup = 23,

    /// <summary>
    /// Вид и универсальность задаются у группы, подкатегория их наследует.
    /// </summary>
    KindInheritedFromGroup = 24,

    /// <summary>
    /// Подкатегория переносится только в группу того же вида.
    /// </summary>
    MoveKeepsKind = 25,

    /// <summary>
    /// Уровень категории не меняется: группа никуда не переносится.
    /// </summary>
    CategoryLevelFixed = 26,

    /// <summary>
    /// У группы ровно один приёмник «Прочее»; у служебной — ровно одна служебная подкатегория.
    /// </summary>
    GroupHasReceiver = 27,

    /// <summary>
    /// Операции удаляемой подкатегории переезжают в приёмник её группы.
    /// </summary>
    DeletedSubcategoryGoesToReceiver = 28,

    /// <summary>
    /// Приёмник и служебные категории не удаляются и не переносятся.
    /// </summary>
    ProtectedCategoryStays = 29,

    /// <summary>
    /// В служебную группу ничего не переносится.
    /// </summary>
    ServiceGroupClosedToMoves = 30,

    /// <summary>
    /// Группа не удаляется: опустевшая остаётся в списке.
    /// </summary>
    GroupNotDeleted = 31,

    // Общее

    /// <summary>
    /// Имя обрезано по краям и не пусто.
    /// </summary>
    NameTrimmedAndNotEmpty = 32,

    /// <summary>
    /// Имя уникально в своей области без учёта регистра и окружающих пробелов.
    /// </summary>
    NameUnique = 33,

    /// <summary>
    /// Удаление только мягкое, и повторное удаление метку не сдвигает.
    /// </summary>
    DeletionIsSoft = 34
}
