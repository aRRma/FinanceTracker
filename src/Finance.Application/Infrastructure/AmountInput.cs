using System.Buffers;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Правила набора выражения с клавиатуры суммы. Отдельно от вычисления: считать
/// можно и то, что набрать нельзя, — выражение приходит ещё и с внешней клавиатуры,
/// а здесь решается, что делает нажатая клавиша.
/// </summary>
/// <remarks>
/// Невозможное нажатие не ошибка и не сигнал: оно просто ничего не меняет.
/// Вторая запятая в числе или действие в пустом поле — промах пальцем, и ругаться
/// на него означало бы мешать вводу ради ничего.
/// </remarks>
public static class AmountInput
{
    /// <summary>Знаки действий клавиатуры — типографские, как на её клавишах.</summary>
    private static readonly SearchValues<char> Operators = SearchValues.Create("+−×÷");

    /// <summary>Разделитель дробной части. На клавиатуре он один — запятая.</summary>
    public const char Separator = ',';

    // Предел суммы — 999 999 999 999,99, тот же, что у Invariant.AmountWithinLimit.
    // Клавиатура не принимает лишние разряды вместо того, чтобы дать набрать
    // и отказать при сохранении
    private const int IntegerDigitLimit = 12;
    private const int FractionDigitLimit = 2;

    /// <summary>Добавляет нажатую клавишу к выражению.</summary>
    /// <param name="expression">Набранное выражение.</param>
    /// <param name="key">Клавиша: цифра, запятая или знак действия.</param>
    /// <returns>Выражение после нажатия. Невозможное нажатие возвращает его без изменений.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Клавиши с таким знаком на клавиатуре нет.</exception>
    public static string Append(string expression, char key)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return key switch
        {
            _ when char.IsAsciiDigit(key) => AppendDigit(expression, key),
            Separator => AppendSeparator(expression),
            _ when Operators.Contains(key) => AppendOperator(expression, key),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Такой клавиши на клавиатуре суммы нет")
        };
    }

    /// <summary>Стирает последний набранный знак.</summary>
    /// <param name="expression">Набранное выражение.</param>
    /// <returns>Выражение без последнего знака. Пустое остаётся пустым.</returns>
    public static string Backspace(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression.Length is 0 ? expression : expression[..^1];
    }

    /// <summary>Цифра: до предела разрядов, но ведущий ноль заменяется, а не наращивается.</summary>
    private static string AppendDigit(string expression, char key)
    {
        ReadOnlySpan<char> number = CurrentNumber(expression);

        // «0» с приписанной цифрой — это та же цифра: «05» никто не имеет в виду
        if (number is ['0'])
        {
            return expression[..^1] + key;
        }

        return Fits(number) ? expression + key : expression;
    }

    /// <summary>Запятая: одна на число и никогда не первой — целая часть обязана быть.</summary>
    private static string AppendSeparator(string expression)
    {
        ReadOnlySpan<char> number = CurrentNumber(expression);

        if (number.Contains(Separator))
        {
            return expression;
        }

        return number.IsEmpty ? expression + "0," : expression + Separator;
    }

    /// <summary>
    /// Знак действия: только после числа. Набранный подряд второй заменяет первый —
    /// пользователь передумал, а не хочет два действия подряд.
    /// </summary>
    private static string AppendOperator(string expression, char key)
    {
        // Хвостовая запятая перед действием отбрасывается: «12,+» не выражение,
        // а «12,» — то же число, что «12»
        string typed = expression.TrimEnd(Separator);

        if (typed.Length is 0)
        {
            return expression;
        }

        if (Operators.Contains(typed[^1]))
        {
            typed = typed[..^1];
        }

        return typed + key;
    }

    /// <summary>Число, которое набирается сейчас, — хвост выражения после последнего действия.</summary>
    private static ReadOnlySpan<char> CurrentNumber(string expression)
    {
        ReadOnlySpan<char> typed = expression;
        int operation = typed.LastIndexOfAny(Operators);

        return operation < 0 ? typed : typed[(operation + 1)..];
    }

    /// <summary>Влезает ли в набираемое число ещё одна цифра.</summary>
    private static bool Fits(ReadOnlySpan<char> number)
    {
        int separator = number.IndexOf(Separator);

        return separator < 0
            ? number.Length < IntegerDigitLimit
            : number.Length - separator - 1 < FractionDigitLimit;
    }
}
