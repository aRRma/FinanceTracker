using Finance.Domain.Errors;

namespace Finance.Domain.Rules;

/// <summary>
/// Границы календарных дат, общие для операции и счёта.
/// </summary>
public static class Dates
{
    /// <summary>
    /// Раньше этой даты учёт не вёлся: такое значение — заведомо опечатка в годе.
    /// Общая граница для даты операции и даты открытия счёта — они сравниваются
    /// между собой, и разные нижние границы разошлись бы бессмысленно.
    /// </summary>
    public static readonly DateOnly Earliest = new(2000, 1, 1);

    /// <summary>
    /// Проверяет, что дата лежит между <see cref="Earliest"/> и сегодняшним днём
    /// включительно.
    /// </summary>
    /// <param name="value">Проверяемая дата.</param>
    /// <param name="today">Локальная дата пользователя, не дата в UTC.</param>
    /// <param name="invariant">Правило, о нарушении которого сообщить.</param>
    /// <param name="what">Что проверяется — попадёт в текст ошибки.</param>
    public static void EnsureInRange(DateOnly value, DateOnly today, Invariant invariant, RuleText what)
    {
        DomainException.ThrowIf(
            value < Earliest,
            invariant,
            RuleText.DateTooEarly,
            what, value, Earliest);

        DomainException.ThrowIf(
            value > today,
            invariant,
            RuleText.DateInFuture,
            what, value, today);
    }
}
