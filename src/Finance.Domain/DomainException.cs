using System.Runtime.CompilerServices;

namespace Finance.Domain;

/// <summary>
/// Нарушение доменного правила. Отдельный тип, чтобы обработчик верхнего уровня
/// отличал «пользователь ввёл недопустимое» от настоящего сбоя и показывал первое
/// как сообщение, а не как падение.
/// </summary>
/// <param name="invariant">Нарушенное правило.</param>
/// <param name="message">Текст для журнала и отладки.</param>
public sealed class DomainException(Invariant invariant, string message) : Exception(message)
{
    /// <summary>
    /// Нарушенное правило. Тесты сверяются с ним, а не с текстом: текст меняется
    /// при переводе, правило — нет.
    /// </summary>
    public Invariant Invariant { get; } = invariant;

    /// <summary>Бросает нарушение правила, если условие выполнено.</summary>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="invariant">Проверяемое правило.</param>
    /// <param name="message">Текст сообщения.</param>
    public static void ThrowIf(bool broken, Invariant invariant, string message)
    {
        if (broken)
        {
            throw new DomainException(invariant, message);
        }
    }

    /// <summary>
    /// Бросает нарушение правила, если условие выполнено, собирая текст только
    /// при нарушении.
    /// </summary>
    /// <remarks>
    /// Перегрузку выбирает компилятор: интерполированная строка идёт сюда, обычная —
    /// в соседний метод. Проверки стоят на каждой сумме и каждой дате, и без этого
    /// текст сообщения собирался бы миллион раз, чтобы не понадобиться ни разу.
    /// </remarks>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="invariant">Проверяемое правило.</param>
    /// <param name="message">Текст сообщения; собирается, только если правило нарушено.</param>
    public static void ThrowIf(
        bool broken,
        Invariant invariant,
        [InterpolatedStringHandlerArgument(nameof(broken))] DomainMessage message)
    {
        if (broken)
        {
            throw new DomainException(invariant, message.ToString());
        }
    }
}
