using System.Buffers;
using System.Globalization;

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
    /// <summary>
    /// Знаки действий клавиатуры — типографские, как на её клавишах.
    /// </summary>
    private static readonly SearchValues<char> Operators = SearchValues.Create("+−×÷");

    /// <summary>
    /// Разделитель дробной части — знак культуры устройства. Клавиша берёт его же:
    /// иначе на английской локали сумма показывалась бы с точкой, а набрать её
    /// было бы нечем.
    /// </summary>
    public static char Separator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

    // Предел суммы — 999 999 999 999,99, тот же, что у Invariant.AmountWithinLimit.
    // Клавиатура не принимает лишние разряды вместо того, чтобы дать набрать
    // и отказать при сохранении
    private const int IntegerDigitLimit = 12;
    private const int FractionDigitLimit = 2;

    /// <summary>
    /// Знак отрицательного числа — тот же типографский минус, что на клавише вычитания.
    /// </summary>
    private const char Minus = '−';

    /// <summary>
    /// Добавляет нажатую клавишу к выражению.
    /// </summary>
    /// <param name="expression">Набранное выражение.</param>
    /// <param name="key">Клавиша: цифра, запятая или знак действия.</param>
    /// <param name="signed">Минус первой клавишей начинает отрицательное число: так набирают долг в начальном остатке. Сумма операции знака не имеет — его задаёт вид.</param>
    /// <returns>Выражение после нажатия. Невозможное нажатие возвращает его без изменений.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Клавиши с таким знаком на клавиатуре нет.</exception>
    public static string Append(string expression, char key, bool signed = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return key switch
        {
            _ when char.IsAsciiDigit(key) => AppendDigit(expression, key),
            _ when key == Separator => AppendSeparator(expression),
            _ when Operators.Contains(key) => AppendOperator(expression, key, signed),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, Faults.KeypadKeyUnknown())
        };
    }

    /// <summary>
    /// Число в том виде, в каком его набирают на клавиатуре: без разделителей разрядов,
    /// без хвоста нулей и с типографским минусом. Набор продолжается с него теми же клавишами.
    /// </summary>
    /// <param name="value">Число.</param>
    /// <returns>Число для поля суммы.</returns>
    public static string Write(decimal value)
    {
        string digits = Math.Abs(value).ToString("0.##", CultureInfo.CurrentCulture);

        return value < 0m ? Minus + digits : digits;
    }

    /// <summary>
    /// Стирает последний набранный знак.
    /// </summary>
    /// <param name="expression">Набранное выражение.</param>
    /// <returns>Выражение без последнего знака. Пустое остаётся пустым.</returns>
    public static string Backspace(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression.Length is 0 ? expression : expression[..^1];
    }

    /// <summary>
    /// Свёрнуто ли выражение до числа. Пока действие не закрыто, итога у набранного
    /// нет: он появляется по клавише «=», а не сам собой.
    /// </summary>
    /// <param name="expression">Набранное выражение.</param>
    /// <returns><c>true</c>, если в выражении есть знак действия. Минус первым — знак числа, а не действие.</returns>
    public static bool HasOperation(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return Unsigned(expression).ContainsAny(Operators);
    }

    /// <summary>
    /// Сворачивает выражение в его итог — это и делает клавиша «=». Незаконченное
    /// выражение остаётся как набрано: нажатию, которому нечего считать, лучше
    /// ничего не менять, чем ругаться.
    /// </summary>
    /// <param name="expression">Набранное выражение.</param>
    /// <returns>Итог числом или само выражение, если оно не вычислимо.</returns>
    public static string Collapse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Итог возвращается в то же поле, и набирать его дальше придётся теми же клавишами
        return AmountExpression.TryEvaluate(expression, out decimal total) ? Write(total) : expression;
    }

    /// <summary>
    /// Цифра: до предела разрядов, но ведущий ноль заменяется, а не наращивается.
    /// </summary>
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

    /// <summary>
    /// Запятая: одна на число и никогда не первой — целая часть обязана быть.
    /// </summary>
    private static string AppendSeparator(string expression)
    {
        ReadOnlySpan<char> number = CurrentNumber(expression);

        if (number.Contains(Separator))
        {
            return expression;
        }

        return number.IsEmpty ? expression + '0' + Separator : expression + Separator;
    }

    /// <summary>
    /// Знак действия: только после числа. Набранный подряд второй заменяет первый —
    /// пользователь передумал, а не хочет два действия подряд. Исключение — минус
    /// в пустом поле, где числу разрешён знак: это не действие, а начало числа.
    /// </summary>
    private static string AppendOperator(string expression, char key, bool signed)
    {
        // Хвостовая запятая перед действием отбрасывается: «12,+» не выражение,
        // а «12,» — то же число, что «12»
        string typed = expression.TrimEnd(Separator);

        if (typed.Length is 0)
        {
            return signed && key is Minus ? Minus.ToString() : expression;
        }

        // Одинокий ноль — то же пустое поле: «0−» никто не набирает ради вычитания
        // из нуля, а ноль остаётся и после «=» над «5−5»
        if (signed && key is Minus && typed is "0")
        {
            return Minus.ToString();
        }

        // Знак без числа заменять нечем: действие требует числа перед собой
        if (typed is [Minus])
        {
            return expression;
        }

        if (Operators.Contains(typed[^1]))
        {
            typed = typed[..^1];
        }

        return typed + key;
    }

    /// <summary>
    /// Число, которое набирается сейчас, — хвост выражения после последнего действия.
    /// Знак первого числа к нему не относится: разряды считаются без него.
    /// </summary>
    private static ReadOnlySpan<char> CurrentNumber(string expression)
    {
        ReadOnlySpan<char> typed = Unsigned(expression);
        int operation = typed.LastIndexOfAny(Operators);

        return operation < 0 ? typed : typed[(operation + 1)..];
    }

    /// <summary>
    /// Выражение без знака первого числа.
    /// </summary>
    private static ReadOnlySpan<char> Unsigned(string expression) =>
        expression.StartsWith(Minus) ? expression.AsSpan(1) : expression;

    /// <summary>
    /// Влезает ли в набираемое число ещё одна цифра.
    /// </summary>
    private static bool Fits(ReadOnlySpan<char> number)
    {
        int separator = number.IndexOf(Separator);

        return separator < 0
            ? number.Length < IntegerDigitLimit
            : number.Length - separator - 1 < FractionDigitLimit;
    }
}
