using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;

namespace Finance.Domain.Rules;

/// <summary>
/// Проверки операции, для которых одной операции мало: они смотрят на счета и
/// категорию. Отдельный тип, а не метод сущности, потому что сущность хранит ключи,
/// а не записи, и дотянуться до них сама не может.
/// </summary>
/// <remarks>
/// Данные подаются уже прочитанными. Домену запрещено ходить в хранилище,
/// а решение при этом остаётся за доменом, а не за обработчиком.
/// Несовпадение переданных записей со ссылками операции — не ввод пользователя,
/// а ошибка вызывающего кода, поэтому <see cref="ArgumentException"/>.
/// </remarks>
public static class TransactionRules
{
    /// <summary>
    /// Проверяет операцию против её счетов и категории.
    /// </summary>
    /// <param name="transaction">Проверяемая операция.</param>
    /// <param name="sourceAccount">Счёт списания.</param>
    /// <param name="targetAccount">Счёт зачисления — у перевода, иначе <c>null</c>.</param>
    /// <param name="category">Подкатегория — у дохода и расхода, иначе <c>null</c>.</param>
    /// <param name="categoryGroup">Группа подкатегории: вид хранится на ней.</param>
    /// <param name="previous">
    /// Состояние той же операции до правки; <c>null</c> означает новую. По нему видно,
    /// какие счета уже были задействованы: закрытый счёт запрещён только в новой операции
    /// и при смене счёта, а уже записанные операции закрытого счёта правятся свободно.
    /// </param>
    public static void EnsureValid(
        Transaction transaction,
        Account sourceAccount,
        Account? targetAccount,
        Category? category,
        Category? categoryGroup,
        Transaction? previous = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(sourceAccount);

        // Чужое прежнее состояние молча снимает запрет на закрытый счёт: его счета
        // сошли бы за «уже задействованные» в операции, которая их впервые видит
        if (previous is not null && previous.Key != transaction.Key)
        {
            throw new ArgumentException(
                DomainFaults.PreviousIsOther(previous.Key, transaction.Key),
                nameof(previous));
        }

        EnsureAccountFits(transaction, transaction.SourceAccountKey, sourceAccount, transaction.Amount, previous);

        // Отдельной проверки «есть ли второй счёт» не нужно: у не-перевода оба
        // значения пусты, и EnsureAccountFits на паре пустых сразу возвращается
        EnsureAccountFits(
            transaction, transaction.TargetAccountKey, targetAccount, transaction.TargetAmount, previous);

        EnsureCategoryFits(transaction, category, categoryGroup);
    }

    /// <summary>
    /// Всё, что проверяется про один счёт операции: тот ли он, в его ли валюте сумма,
    /// не закрыт ли он и не раньше ли операция его открытия.
    /// </summary>
    private static void EnsureAccountFits(
        Transaction transaction,
        Guid? expectedKey,
        Account? account,
        Money? amount,
        Transaction? previous)
    {
        if (expectedKey != account?.Key)
        {
            throw new ArgumentException(
                DomainFaults.AccountMismatch(account?.Key, expectedKey),
                nameof(account));
        }

        if (account is null)
        {
            return;
        }

        // Своей колонки валюта у операции не имеет и берётся у счёта, поэтому
        // расхождение означает, что сумму собрали не из той валюты
        if (amount is { } value && value.Currency != account.Currency)
        {
            throw new ArgumentException(
                DomainFaults.AmountNotInAccountCurrency(value, account.Name, account.Currency),
                nameof(transaction));
        }

        bool wasAlreadyUsed = account.Key == previous?.SourceAccountKey
                              || account.Key == previous?.TargetAccountKey;

        DomainException.ThrowIf(
            account.IsClosed && !wasAlreadyUsed,
            Invariant.ClosedAccountNotInNewTransaction,
            RuleText.ClosedAccountNotInNewTransaction,
            account.Name);

        DomainException.ThrowIf(
            transaction.OccurredOn < account.OpenedOn,
            Invariant.TransactionNotBeforeAccountOpened,
            RuleText.TransactionNotBeforeAccountOpened,
            transaction.OccurredOn, account.Name, account.OpenedOn);
    }

    /// <summary>
    /// Категория второго уровня и того же вида, что операция.
    /// </summary>
    private static void EnsureCategoryFits(Transaction transaction, Category? category, Category? categoryGroup)
    {
        if (transaction.CategoryKey is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(categoryGroup);

        if (category.Key != transaction.CategoryKey)
        {
            throw new ArgumentException(
                DomainFaults.CategoryMismatch(category.Key, transaction.CategoryKey),
                nameof(category));
        }

        DomainException.ThrowIf(
            !category.IsSubcategory,
            Invariant.CategoryIsSubcategory,
            RuleText.CategoryIsSubcategory,
            category.Name);

        if (category.ParentKey != categoryGroup.Key)
        {
            throw new ArgumentException(
                DomainFaults.NotGroupOfCategory(categoryGroup.Name, category.Name),
                nameof(categoryGroup));
        }

        CategoryKind expected = transaction.Kind is TransactionKind.Income
            ? CategoryKind.Income
            : CategoryKind.Expense;

        // Универсальная группа принимает оба вида: возврат в магазине, кэшбэк
        // и правка расхождения ложатся в ту же статью, где лежит трата,
        // и в отчёте вычитаются из неё, а не заводят доход на пустом месте
        DomainException.ThrowIf(
            !categoryGroup.Accepts(expected),
            Invariant.CategoryKindMatchesTransaction,
            RuleText.CategoryKindMatchesTransaction,
            transaction.Kind, categoryGroup.Name, categoryGroup.Kind);
    }
}
