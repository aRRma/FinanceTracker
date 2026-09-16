using System.Buffers;
using System.Globalization;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Поле суммы принимает арифметическое выражение из четырёх действий. Считается
/// слева направо без приоритета умножения: клавиатура в форме — это калькулятор
/// на чеке, а не запись формулы, и «1250 + 340 * 2» здесь значит то же,
/// что показал бы карманный калькулятор.
/// </summary>
public static class AmountExpression
{
    /// <summary>
    /// Знаки действий. С клавиатуры формы приходят типографские, с внешней — обычные.
    /// </summary>
    private static readonly SearchValues<char> Operators = SearchValues.Create("+-*/×÷−");

    /// <summary>
    /// Разбор числа с запятой — так его набирают на клавиатуре формы.
    /// </summary>
    private static readonly NumberFormatInfo CommaSeparator = new() { NumberDecimalSeparator = "," };

    private const int Scale = 2;

    /// <summary>
    /// Вычисляет выражение. Незаконченное выражение — не ошибка ввода, а ещё
    /// не набранное значение: результат просто не показывается.
    /// </summary>
    /// <param name="text">Содержимое поля суммы.</param>
    /// <param name="result">Результат, округлённый до копеек.</param>
    /// <returns><c>true</c>, если выражение законченное и вычислимое.</returns>
    public static bool TryEvaluate(ReadOnlySpan<char> text, out decimal result)
    {
        result = 0m;

        // Знак допустим только у первого числа: дальше минус — это действие,
        // иначе «1250-340» читалось бы как два числа подряд
        if (!TryReadNumber(ref text, allowSign: true, out decimal accumulated))
        {
            return false;
        }

        while (true)
        {
            text = text.TrimStart();

            if (text.IsEmpty)
            {
                return TryRound(accumulated, out result);
            }

            char operation = text[0];

            if (!Operators.Contains(operation))
            {
                return false;
            }

            text = text[1..];

            if (!TryReadNumber(ref text, allowSign: false, out decimal operand)
                || !TryApply(operation, accumulated, operand, out accumulated))
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Округляет итог до копеек. Переполнение здесь — такой же незаконченный ввод,
    /// как деление на ноль: <c>Try</c>-метод не имеет права бросать, а вызывающий
    /// ловит только доменные исключения и на <see cref="OverflowException"/> упал бы.
    /// </summary>
    private static bool TryRound(decimal value, out decimal result)
    {
        try
        {
            result = decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

            return true;
        }
        catch (OverflowException)
        {
            result = 0m;

            return false;
        }
    }

    /// <summary>
    /// Применяет действие. Деление на ноль и переполнение — не исключение, а незаконченный ввод.
    /// </summary>
    private static bool TryApply(char operation, decimal left, decimal right, out decimal value)
    {
        try
        {
            switch (operation)
            {
                case '+':
                    value = left + right;

                    return true;

                case '-' or '−':
                    value = left - right;

                    return true;

                case '*' or '×':
                    value = left * right;

                    return true;

                case '/' or '÷' when right != 0m:
                    value = left / right;

                    return true;

                default:
                    value = 0m;

                    return false;
            }
        }
        catch (OverflowException)
        {
            value = 0m;

            return false;
        }
    }

    /// <summary>
    /// Читает число, отрезая его от начала выражения. Разделитель принимается любой:
    /// на клавиатуре формы запятая, на внешней клавиатуре чаще точка.
    /// </summary>
    /// <param name="text">Остаток выражения; читаемое число из него убирается.</param>
    /// <param name="allowSign">Принимать ведущий минус: начальный остаток бывает отрицательным.</param>
    /// <param name="value">Прочитанное число.</param>
    private static bool TryReadNumber(ref ReadOnlySpan<char> text, bool allowSign, out decimal value)
    {
        value = 0m;
        text = text.TrimStart();

        int length = 0;
        bool separatorSeen = false;
        bool negative = false;

        if (allowSign && text.Length > 0 && text[0] is '-' or '−')
        {
            negative = true;
            length = 1;
        }

        int digits = 0;

        while (length < text.Length)
        {
            char symbol = text[length];

            if (char.IsAsciiDigit(symbol))
            {
                digits++;
                length++;
            }
            else if (symbol is '.' or ',' && !separatorSeen)
            {
                separatorSeen = true;
                length++;
            }
            else
            {
                break;
            }
        }

        if (digits == 0)
        {
            return false;
        }

        // Знак снимается перед разбором: NumberStyles без AllowLeadingSign
        // отвергло бы минус, а включать его значило бы принимать «+5» в середине
        ReadOnlySpan<char> number = negative ? text[1..length] : text[..length];
        text = text[length..];

        // Две попытки вместо переписывания разделителя в буфер: культура устройства
        // не должна решать, что значит запятая в сумме, а строку ради этого
        // выделять незачем
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
            && !decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CommaSeparator, out value))
        {
            return false;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }
}
