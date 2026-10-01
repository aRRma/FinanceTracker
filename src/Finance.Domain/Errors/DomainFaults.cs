namespace Finance.Domain.Errors;

/// <summary>
/// Тексты ошибок вызывающего кода: переданы не те записи, ключ пуст, прежнее
/// состояние от другой операции. Это не ввод пользователя и не нарушенное
/// правило — читает их тот, кто читает журнал или падающий тест.
/// </summary>
/// <remarks>
/// Намеренно мимо ресурсов текста: наружу такие сообщения не выходят, переводить
/// их некому. Доменные правила, которые пользователь видит, лежат
/// в <see cref="RuleTexts"/>.
/// </remarks>
internal static class DomainFaults
{
    /// <summary>
    /// Ключ передан пустым: форма отдала невыбранное значение как <c>default</c>.
    /// </summary>
    internal static string KeyIsEmpty() => "Ключ не может быть пустым: он ни на что не ссылается";

    /// <summary>
    /// Счёт удаляют общим путём сущности, минуя проверку операций.
    /// </summary>
    internal static string AccountDeleteNeedsTransactions() =>
        "Счёт удаляется только перегрузкой с признаком операций: общий путь пропустил бы проверку";

    /// <summary>
    /// Переданная категория — не группа.
    /// </summary>
    /// <param name="name">Название категории.</param>
    internal static string NotAGroup(string name) => $"«{name}» не группа";

    /// <summary>
    /// Подкатегория передана вместе с чужой группой.
    /// </summary>
    /// <param name="subcategory">Название подкатегории.</param>
    /// <param name="group">Название группы.</param>
    internal static string NotInGroup(string subcategory, string group) =>
        $"«{subcategory}» не принадлежит группе «{group}»";

    /// <summary>
    /// Прежнее состояние относится к другой операции: чужое прежнее состояние
    /// молча снимает запрет на заблокированный счёт.
    /// </summary>
    /// <param name="previous">Ключ операции из прежнего состояния.</param>
    /// <param name="edited">Ключ правимой операции.</param>
    internal static string PreviousIsOther(Guid previous, Guid edited) =>
        $"Прежнее состояние относится к операции {previous}, а правится {edited}";

    /// <summary>
    /// Передан не тот счёт, на который ссылается операция.
    /// </summary>
    /// <param name="given">Ключ переданного счёта.</param>
    /// <param name="expected">Ключ, на который ссылается операция.</param>
    internal static string AccountMismatch(Guid? given, Guid? expected) =>
        $"Передан счёт {given?.ToString() ?? "null"}, а операция ссылается на {expected?.ToString() ?? "null"}";

    /// <summary>
    /// Сумма собрана не в валюте своего счёта: своей колонки валюта у операции
    /// не имеет и берётся у счёта.
    /// </summary>
    /// <param name="amount">Сумма.</param>
    /// <param name="account">Название счёта.</param>
    /// <param name="currency">Валюта счёта.</param>
    internal static string AmountNotInAccountCurrency(object amount, string account, object currency) =>
        $"Сумма {amount} не в валюте счёта «{account}» ({currency})";

    /// <summary>
    /// Передана не та категория, на которую ссылается операция.
    /// </summary>
    /// <param name="given">Ключ переданной категории.</param>
    /// <param name="expected">Ключ, на который ссылается операция.</param>
    internal static string CategoryMismatch(Guid given, Guid? expected) =>
        $"Передана категория {given}, а операция ссылается на {expected}";

    /// <summary>
    /// Передана не та группа, которой принадлежит подкатегория.
    /// </summary>
    /// <param name="group">Название переданной группы.</param>
    /// <param name="category">Название подкатегории.</param>
    internal static string NotGroupOfCategory(string group, string category) =>
        $"«{group}» не является группой подкатегории «{category}»";
}
