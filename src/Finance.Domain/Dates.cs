namespace Finance.Domain;

/// <summary>Границы календарных дат, общие для операции и счёта.</summary>
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
    public static void EnsureInRange(DateOnly value, DateOnly today, Invariant invariant, string what)
    {
        DomainException.ThrowIf(
            value < Earliest,
            invariant,
            $"{what} {value:yyyy-MM-dd} раньше {Earliest:yyyy-MM-dd}");

        DomainException.ThrowIf(
            value > today,
            invariant,
            $"{what} {value:yyyy-MM-dd} в будущем: сегодня {today:yyyy-MM-dd}");
    }
}
