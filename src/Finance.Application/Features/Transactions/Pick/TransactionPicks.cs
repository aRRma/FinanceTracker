namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Что выбрали на экране выбора. Экран кладёт сюда результат и уходит назад,
/// форма операции забирает его при появлении и очищает.
/// <para>
/// Возврат значения параметром маршрута не годится: имя нового места — кириллица,
/// а параметр к тому же остаётся в свойстве страницы до следующего перехода
/// и подставился бы ещё раз. Здесь значение забирают один раз, и проверяется это
/// обычным тестом, без эмулятора.
/// </para>
/// </summary>
public sealed class TransactionPicks
{
    /// <summary>
    /// Выбранный счёт списания.
    /// </summary>
    public Guid? Account { get; set; }

    /// <summary>
    /// Выбранный счёт зачисления — у перевода.
    /// </summary>
    public Guid? TargetAccount { get; set; }

    /// <summary>
    /// Выбранная подкатегория.
    /// </summary>
    public Guid? Category { get; set; }

    /// <summary>
    /// Выбранное или набранное место. Пустая строка — «без места».
    /// </summary>
    public string? PlaceName { get; set; }

    /// <summary>
    /// Забирает выбор: всё, что положил экран, достаётся ровно один раз.
    /// </summary>
    public void Clear()
    {
        Account = null;
        TargetAccount = null;
        Category = null;
        PlaceName = null;
    }
}
