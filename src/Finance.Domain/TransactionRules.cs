namespace Finance.Domain;

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
                $"Прежнее состояние относится к операции {previous.Key}, а правится {transaction.Key}",
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
                $"Передан счёт {account?.Key.ToString() ?? "null"}, " +
                $"а операция ссылается на {expectedKey?.ToString() ?? "null"}",
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
                $"Сумма {value} не в валюте счёта «{account.Name}» ({account.Currency})",
                nameof(transaction));
        }

        bool wasAlreadyUsed = account.Key == previous?.SourceAccountKey
                              || account.Key == previous?.TargetAccountKey;

        DomainException.ThrowIf(
            account.IsClosed && !wasAlreadyUsed,
            Invariant.ClosedAccountNotInNewTransaction,
            $"Счёт «{account.Name}» закрыт и в новой операции использован быть не может");

        DomainException.ThrowIf(
            transaction.OccurredOn < account.OpenedOn,
            Invariant.TransactionNotBeforeAccountOpened,
            $"Дата операции {transaction.OccurredOn:yyyy-MM-dd} раньше открытия счёта " +
            $"«{account.Name}» ({account.OpenedOn:yyyy-MM-dd})");
    }

    /// <summary>Категория второго уровня и того же вида, что операция.</summary>
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
                $"Передана категория {category.Key}, а операция ссылается на {transaction.CategoryKey}",
                nameof(category));
        }

        DomainException.ThrowIf(
            !category.IsSubcategory,
            Invariant.CategoryIsSubcategory,
            $"«{category.Name}» — группа: в операции указывается подкатегория");

        if (category.ParentKey != categoryGroup.Key)
        {
            throw new ArgumentException(
                $"«{categoryGroup.Name}» не является группой подкатегории «{category.Name}»",
                nameof(categoryGroup));
        }

        CategoryKind expected = transaction.Kind is TransactionKind.Income
            ? CategoryKind.Income
            : CategoryKind.Expense;

        DomainException.ThrowIf(
            categoryGroup.Kind != expected,
            Invariant.CategoryKindMatchesTransaction,
            $"Операция вида {transaction.Kind} не относится к группе «{categoryGroup.Name}» вида {categoryGroup.Kind}");
    }
}
