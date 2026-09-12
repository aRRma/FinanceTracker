namespace Finance.Domain;

/// <summary>
/// Правила, которые доменные типы соблюдают всегда. Имя члена говорит само за себя,
/// чтобы тест и сообщение об ошибке читались без справочника требований.
/// </summary>
/// <remarks>
/// Часть правил проверяется исключением, часть соблюдается самим устройством типа —
/// у них нет <see cref="DomainException"/>, но есть тест. Числовые значения ничего
/// не значат и нигде не хранятся: порядок членов можно менять свободно.
/// </remarks>
public enum Invariant
{
    // Суммы

    /// <summary>Сумма всегда положительна, знак в балансе задаёт вид операции.</summary>
    AmountSignComesFromKind,

    /// <summary>Сумма операции строго больше нуля.</summary>
    AmountIsPositive,

    /// <summary>Сумма по модулю не превышает <see cref="Money.Limit"/>.</summary>
    AmountWithinLimit,

    /// <summary>Сумма выражена в целых копейках: не больше двух знаков после запятой.</summary>
    AmountInWholeKopecks,

    /// <summary>Суммы в разных валютах не складываются, не вычитаются и не сравниваются.</summary>
    CurrenciesNeverMixed,

    // Операция

    /// <summary>Счёт и сумма зачисления есть у перевода и только у него.</summary>
    TargetOnlyInTransfer,

    /// <summary>Перевод идёт между разными счетами.</summary>
    TransferAccountsDiffer,

    /// <summary>Перевод внутри одной валюты списывает и зачисляет одну и ту же сумму.</summary>
    SameCurrencyTransferAmountsEqual,

    /// <summary>Категория обязательна для дохода и расхода и запрещена для перевода.</summary>
    CategoryOnlyInIncomeAndExpense,

    /// <summary>Операция относится к подкатегории, а не к группе.</summary>
    CategoryIsSubcategory,

    /// <summary>Вид группы категории совпадает с видом операции.</summary>
    CategoryKindMatchesTransaction,

    /// <summary>Место необязательно для дохода и расхода и запрещено для перевода.</summary>
    PlaceNotInTransfer,

    /// <summary>Дата операции не раньше открытия каждого её счёта.</summary>
    TransactionNotBeforeAccountOpened,

    /// <summary>Дата операции — от <see cref="Dates.Earliest"/> до сегодняшнего дня включительно.</summary>
    TransactionDateInRange,

    /// <summary>Заметка не длиннее <see cref="Transaction.MaxNoteLength"/>.</summary>
    NoteWithinLimit,

    /// <summary>Закрытый счёт не попадает в новую операцию и не подставляется при правке.</summary>
    ClosedAccountNotInNewTransaction,

    /// <summary>Правка операции заменяет её состояние целиком, включая вид.</summary>
    EditReplacesWholeState,

    /// <summary>Смена вида сразу стирает поля, которых у нового вида не бывает.</summary>
    KindChangeClearsForbiddenFields,

    // Счёт

    /// <summary>Валюта счёта не меняется, если по нему когда-либо была операция.</summary>
    CurrencyFixedOnceUsed,

    /// <summary>Дату открытия нельзя сдвинуть позже самой ранней операции.</summary>
    OpenedOnNotAfterTransactions,

    /// <summary>Дата открытия счёта — в тех же границах, что и дата операции.</summary>
    OpeningDateInRange,

    // Категории

    /// <summary>Уровней категорий ровно два: группа и подкатегория.</summary>
    TwoCategoryLevels,

    /// <summary>Подкатегория ссылается на группу, группа — ни на что.</summary>
    SubcategoryBelongsToGroup,

    /// <summary>Вид задаётся у группы, подкатегория его наследует.</summary>
    KindInheritedFromGroup,

    /// <summary>Подкатегория переносится только в группу того же вида.</summary>
    MoveKeepsKind,

    /// <summary>Уровень категории не меняется: группа никуда не переносится.</summary>
    CategoryLevelFixed,

    /// <summary>У группы ровно один приёмник «Прочее»; у служебной — ровно одна служебная подкатегория.</summary>
    GroupHasReceiver,

    /// <summary>Операции удаляемой подкатегории переезжают в приёмник её группы.</summary>
    DeletedSubcategoryGoesToReceiver,

    /// <summary>Приёмник и служебные категории не удаляются и не переносятся.</summary>
    ProtectedCategoryStays,

    /// <summary>В служебную группу ничего не переносится.</summary>
    ServiceGroupClosedToMoves,

    /// <summary>Группа не удаляется: опустевшая остаётся в списке.</summary>
    GroupNotDeleted,

    // Общее

    /// <summary>Имя обрезано по краям и не пусто.</summary>
    NameTrimmedAndNotEmpty,

    /// <summary>Имя уникально в своей области без учёта регистра и окружающих пробелов.</summary>
    NameUnique,

    /// <summary>Удаление только мягкое, и повторное удаление метку не сдвигает.</summary>
    DeletionIsSoft
}
