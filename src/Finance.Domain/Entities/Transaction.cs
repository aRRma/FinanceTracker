using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Rules;
using Finance.Domain.Values;

namespace Finance.Domain.Entities;

/// <summary>
/// Операция: доход, расход или перевод. Сумма всегда положительна, знак задаёт вид —
/// расход, записанный отрицательным числом, уехал бы в баланс дважды в одну сторону,
/// и ошибка размазалась бы по всей истории.
/// </summary>
public sealed class Transaction : Entity
{
    /// <summary>
    /// Предел длины заметки.
    /// </summary>
    public const int MaxNoteLength = 1000;

    private Transaction(
        Guid key,
        TransactionKind kind,
        Guid sourceAccountKey,
        Guid? targetAccountKey,
        Money amount,
        Money? targetAmount,
        Guid? categoryKey,
        Guid? placeKey,
        DateOnly occurredOn,
        string? note,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId)
        : base(key, createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId)
    {
        Kind = kind;
        SourceAccountKey = sourceAccountKey;
        TargetAccountKey = targetAccountKey;
        Amount = amount;
        TargetAmount = targetAmount;
        CategoryKey = categoryKey;
        PlaceKey = placeKey;
        OccurredOn = occurredOn;
        Note = note;
    }

    /// <summary>
    /// Вид операции. Меняется при правке наравне с прочими полями.
    /// </summary>
    public TransactionKind Kind { get; private set; }

    /// <summary>
    /// Счёт списания. У дохода и расхода — единственный счёт операции.
    /// </summary>
    public Guid SourceAccountKey { get; private set; }

    /// <summary>
    /// Счёт зачисления. Только у перевода.
    /// </summary>
    public Guid? TargetAccountKey { get; private set; }

    /// <summary>
    /// Сумма в валюте счёта списания. Всегда положительна.
    /// </summary>
    public Money Amount { get; private set; }

    /// <summary>
    /// Сумма зачисления в валюте счёта зачисления. Есть у любого перевода, а не только
    /// у перевода между валютами: при одной валюте равна сумме списания.
    /// </summary>
    public Money? TargetAmount { get; private set; }

    /// <summary>
    /// Подкатегория. Обязательна для дохода и расхода, запрещена для перевода.
    /// </summary>
    public Guid? CategoryKey { get; private set; }

    /// <summary>
    /// Место. Необязательно, у перевода запрещено.
    /// </summary>
    public Guid? PlaceKey { get; private set; }

    /// <summary>
    /// Календарная дата операции. Без времени и часового пояса.
    /// </summary>
    public DateOnly OccurredOn { get; private set; }

    /// <summary>
    /// Заметка. Пустая не хранится: пустая строка и её отсутствие — одно и то же.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Операция — перевод между своими счетами.
    /// </summary>
    public bool IsTransfer => Kind is TransactionKind.Transfer;

    /// <summary>
    /// Записывает операцию. Проверки, требующие счетов и категории, выполняет
    /// <see cref="TransactionRules"/>: здесь их сделать нечем — сущность хранит ключи, а не записи.
    /// </summary>
    /// <param name="kind">Вид операции.</param>
    /// <param name="sourceAccountKey">Счёт списания.</param>
    /// <param name="amount">Сумма в валюте счёта списания.</param>
    /// <param name="targetAccountKey">Счёт зачисления — только у перевода.</param>
    /// <param name="targetAmount">Сумма зачисления — только у перевода.</param>
    /// <param name="categoryKey">Подкатегория — у дохода и расхода.</param>
    /// <param name="placeKey">Место — необязательно, кроме перевода.</param>
    /// <param name="occurredOn">Дата операции.</param>
    /// <param name="note">Заметка.</param>
    /// <param name="today">Локальная дата пользователя, не дата в UTC.</param>
    /// <param name="nowUtc">Момент записи.</param>
    public static Transaction Create(
        TransactionKind kind,
        Guid sourceAccountKey,
        Money amount,
        Guid? targetAccountKey,
        Money? targetAmount,
        Guid? categoryKey,
        Guid? placeKey,
        DateOnly occurredOn,
        string? note,
        DateOnly today,
        DateTimeOffset nowUtc)
    {
        string? cleanNote = Validate(
            kind, sourceAccountKey, amount, targetAccountKey, targetAmount,
            categoryKey, placeKey, occurredOn, note, today);

        return new Transaction(
            Keys.New(), kind, sourceAccountKey, targetAccountKey, amount, targetAmount,
            categoryKey, placeKey, occurredOn, cleanNote,
            createdAtUtc: nowUtc, updatedAtUtc: nowUtc,
            deletedAtUtc: null, syncedAtUtc: null, externalId: null);
    }

    /// <summary>
    /// ВОССТАНОВЛЕНИЕ ИЗ ХРАНИЛИЩА. Инварианты не проверяются: строка в базе уже
    /// прошла проверку при вводе, а повторная превратила бы чтение в валидацию.
    /// Для записи операции этот путь не годится — есть <see cref="Create"/>.
    /// </summary>
    public static Transaction Restore(
        Guid key,
        TransactionKind kind,
        Guid sourceAccountKey,
        Guid? targetAccountKey,
        Money amount,
        Money? targetAmount,
        Guid? categoryKey,
        Guid? placeKey,
        DateOnly occurredOn,
        string? note,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc,
        DateTimeOffset? syncedAtUtc,
        string? externalId) =>
        new(key, kind, sourceAccountKey, targetAccountKey, amount, targetAmount,
            categoryKey, placeKey, occurredOn, note,
            createdAtUtc, updatedAtUtc, deletedAtUtc, syncedAtUtc, externalId);

    /// <summary>
    /// Заменяет состояние операции целиком, включая вид. Частичной правки нет
    /// намеренно: поля, запрещённые новым видом, обязаны исчезнуть сразу, а не
    /// сохраниться скрытым состоянием до возврата прежнего вида.
    /// </summary>
    /// <param name="kind">Вид операции.</param>
    /// <param name="sourceAccountKey">Счёт списания.</param>
    /// <param name="amount">Сумма в валюте счёта списания.</param>
    /// <param name="targetAccountKey">Счёт зачисления — только у перевода.</param>
    /// <param name="targetAmount">Сумма зачисления — только у перевода.</param>
    /// <param name="categoryKey">Подкатегория — у дохода и расхода.</param>
    /// <param name="placeKey">Место — необязательно, кроме перевода.</param>
    /// <param name="occurredOn">Дата операции.</param>
    /// <param name="note">Заметка.</param>
    /// <param name="today">Локальная дата пользователя, не дата в UTC.</param>
    public void Replace(
        TransactionKind kind,
        Guid sourceAccountKey,
        Money amount,
        Guid? targetAccountKey,
        Money? targetAmount,
        Guid? categoryKey,
        Guid? placeKey,
        DateOnly occurredOn,
        string? note,
        DateOnly today)
    {
        string? cleanNote = Validate(
            kind, sourceAccountKey, amount, targetAccountKey, targetAmount,
            categoryKey, placeKey, occurredOn, note, today);

        Kind = kind;
        SourceAccountKey = sourceAccountKey;
        TargetAccountKey = targetAccountKey;
        Amount = amount;
        TargetAmount = targetAmount;
        CategoryKey = categoryKey;
        PlaceKey = placeKey;
        OccurredOn = occurredOn;
        Note = cleanNote;
    }

    /// <summary>
    /// Возвращает приведённую заметку и бросает исключение на первом нарушенном правиле.
    /// </summary>
    private static string? Validate(
        TransactionKind kind,
        Guid sourceAccountKey,
        Money amount,
        Guid? targetAccountKey,
        Money? targetAmount,
        Guid? categoryKey,
        Guid? placeKey,
        DateOnly occurredOn,
        string? note,
        DateOnly today)
    {
        // Не доменные правила, а ошибки вызывающего кода: пустой ключ означает, что форма
        // отдала невыбранное значение как default(Guid) вместо null, и операция
        // сошлётся на несуществующий счёт, категорию или место
        EnsureRealKey(sourceAccountKey, nameof(sourceAccountKey));
        EnsureRealKey(targetAccountKey, nameof(targetAccountKey));
        EnsureRealKey(categoryKey, nameof(categoryKey));
        EnsureRealKey(placeKey, nameof(placeKey));

        bool isTransfer = kind is TransactionKind.Transfer;

        EnsureAmount(amount, RuleText.SubjectTransactionAmount);
        EnsureTransferShape(isTransfer, sourceAccountKey, targetAccountKey, amount, targetAmount);
        EnsureCategoryAndPlace(isTransfer, categoryKey, placeKey);

        Dates.EnsureInRange(occurredOn, today, Invariant.TransactionDateInRange, RuleText.SubjectTransactionDate);

        return CleanNote(note);
    }

    /// <summary>
    /// Сумма строго больше нуля и не превышает предела.
    /// </summary>
    private static void EnsureAmount(Money amount, RuleText what)
    {
        DomainException.ThrowIf(
            !amount.IsPositive,
            Invariant.AmountIsPositive,
            RuleText.AmountIsPositive,
            what, amount);

        amount.EnsureWithinLimit(what);
    }

    /// <summary>
    /// Поля, существующие ровно у перевода: второй счёт и вторая сумма.
    /// </summary>
    private static void EnsureTransferShape(
        bool isTransfer,
        Guid sourceAccountKey,
        Guid? targetAccountKey,
        Money amount,
        Money? targetAmount)
    {
        // «Тогда и только тогда». Заполнены они и при совпадающих валютах,
        // просто равны сумме списания
        DomainException.ThrowIf(
            isTransfer != targetAccountKey.HasValue,
            Invariant.TargetOnlyInTransfer,
            isTransfer ? RuleText.TransferNeedsTargetAccount : RuleText.TargetAccountOnlyInTransfer);

        DomainException.ThrowIf(
            isTransfer != targetAmount.HasValue,
            Invariant.TargetOnlyInTransfer,
            isTransfer ? RuleText.TransferNeedsTargetAmount : RuleText.TargetAmountOnlyInTransfer);

        // Суммы зачисления нет ровно у дохода и расхода — проверено выше
        if (targetAmount is not { } target)
        {
            return;
        }

        DomainException.ThrowIf(
            targetAccountKey == sourceAccountKey,
            Invariant.TransferAccountsDiffer,
            RuleText.TransferAccountsDiffer);

        EnsureAmount(target, RuleText.SubjectTargetAmount);

        // Одна валюта — одна сумма. Курса здесь быть не может, а разные
        // числа означали бы, что деньги по дороге появились или исчезли
        DomainException.ThrowIf(
            target.Currency == amount.Currency && target.Amount != amount.Amount,
            Invariant.SameCurrencyTransferAmountsEqual,
            RuleText.SameCurrencyTransferAmountsEqual,
            amount, target);
    }

    /// <summary>
    /// У перевода нет ни категории, ни места — он не расход и не доход,
    /// и относить его «на что» и «где» не к чему.
    /// </summary>
    private static void EnsureCategoryAndPlace(bool isTransfer, Guid? categoryKey, Guid? placeKey)
    {
        DomainException.ThrowIf(
            isTransfer == categoryKey.HasValue,
            Invariant.CategoryOnlyInIncomeAndExpense,
            isTransfer ? RuleText.CategoryNotInTransfer : RuleText.CategoryRequiredInIncomeAndExpense);

        DomainException.ThrowIf(
            isTransfer && placeKey.HasValue,
            Invariant.PlaceNotInTransfer,
            RuleText.PlaceNotInTransfer);
    }

    /// <summary>
    /// Заметка не длиннее предела. Пустая не хранится — это то же, что её отсутствие.
    /// </summary>
    private static string? CleanNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        string trimmed = note.Trim();

        DomainException.ThrowIf(
            trimmed.Length > MaxNoteLength,
            Invariant.NoteWithinLimit,
            RuleText.NoteWithinLimit,
            trimmed.Length, MaxNoteLength);

        return trimmed;
    }

    /// <summary>
    /// Ключ либо отсутствует, либо настоящий: <c>Guid.Empty</c> не ссылается ни на что.
    /// </summary>
    private static void EnsureRealKey(Guid? key, string parameterName)
    {
        if (key == Guid.Empty)
        {
            throw new ArgumentException(DomainFaults.KeyIsEmpty(), parameterName);
        }
    }
}
