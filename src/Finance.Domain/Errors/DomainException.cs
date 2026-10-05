using System.Runtime.CompilerServices;

namespace Finance.Domain.Errors;

/// <summary>
/// Нарушение доменного правила. Отдельный тип, чтобы обработчик верхнего уровня
/// отличал «пользователь ввёл недопустимое» от настоящего сбоя и показывал первое
/// как сообщение, а не как падение.
/// </summary>
/// <param name="invariant">Нарушенное правило.</param>
/// <param name="message">Текст для пользователя рядом с формой — его словами: счёт, сумма или дата, а не имя поля или правила.</param>
public sealed class DomainException(Invariant invariant, string message) : Exception(message)
{
    /// <summary>
    /// Нарушенное правило. Тесты сверяются с ним, а не с текстом: текст меняется
    /// при переводе, правило — нет.
    /// </summary>
    public Invariant Invariant { get; } = invariant;

    /// <summary>
    /// Бросает нарушение правила, если условие выполнено.
    /// </summary>
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
    /// Бросает нарушение правила с текстом без подстановок.
    /// </summary>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="invariant">Проверяемое правило.</param>
    /// <param name="text">Ключ текста сообщения.</param>
    public static void ThrowIf(bool broken, Invariant invariant, RuleText text)
    {
        if (broken)
        {
            throw new DomainException(invariant, RuleTexts.Of(text));
        }
    }

    /// <summary>
    /// Бросает нарушение правила, подставляя в текст одно значение.
    /// </summary>
    /// <remarks>
    /// Значение принимается обобщённым параметром, а не через <c>object</c>
    /// и не через <c>params</c>: на счастливом пути тогда нет ни упаковки,
    /// ни массива, ни обращения к ресурсу — проверки стоят на каждой сумме
    /// и каждой дате.
    /// </remarks>
    /// <typeparam name="T1">Тип подставляемого значения.</typeparam>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="invariant">Проверяемое правило.</param>
    /// <param name="text">Ключ текста сообщения.</param>
    /// <param name="first">Первая подстановка.</param>
    public static void ThrowIf<T1>(bool broken, Invariant invariant, RuleText text, T1 first)
    {
        if (broken)
        {
            throw new DomainException(invariant, RuleTexts.Format(text, first));
        }
    }

    /// <summary>
    /// Бросает нарушение правила, подставляя в текст два значения.
    /// </summary>
    /// <typeparam name="T1">Тип первого значения.</typeparam>
    /// <typeparam name="T2">Тип второго значения.</typeparam>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="invariant">Проверяемое правило.</param>
    /// <param name="text">Ключ текста сообщения.</param>
    /// <param name="first">Первая подстановка.</param>
    /// <param name="second">Вторая подстановка.</param>
    public static void ThrowIf<T1, T2>(bool broken, Invariant invariant, RuleText text, T1 first, T2 second)
    {
        if (broken)
        {
            throw new DomainException(invariant, RuleTexts.Format(text, first, second));
        }
    }

    /// <summary>
    /// Бросает нарушение правила, подставляя в текст три значения.
    /// </summary>
    /// <typeparam name="T1">Тип первого значения.</typeparam>
    /// <typeparam name="T2">Тип второго значения.</typeparam>
    /// <typeparam name="T3">Тип третьего значения.</typeparam>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="invariant">Проверяемое правило.</param>
    /// <param name="text">Ключ текста сообщения.</param>
    /// <param name="first">Первая подстановка.</param>
    /// <param name="second">Вторая подстановка.</param>
    /// <param name="third">Третья подстановка.</param>
    public static void ThrowIf<T1, T2, T3>(
        bool broken,
        Invariant invariant,
        RuleText text,
        T1 first,
        T2 second,
        T3 third)
    {
        if (broken)
        {
            throw new DomainException(invariant, RuleTexts.Format(text, first, second, third));
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
