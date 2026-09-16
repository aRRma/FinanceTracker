namespace Finance.Application.Infrastructure;

/// <summary>
/// Русское склонение счётных форм: «1 операция», «2 операции», «5 операций».
/// Нужно подписям справочников и диалогу удаления — без него они писали бы
/// «операций: 1», а это язык таблицы, а не язык предупреждения.
/// </summary>
public static class Plural
{
    /// <summary>
    /// Собирает число со склонённым словом.
    /// </summary>
    /// <param name="count">Количество.</param>
    /// <param name="one">Форма при одном: «операция перейдёт».</param>
    /// <param name="few">Форма при двух-четырёх: «операции перейдут».</param>
    /// <param name="many">Форма при пяти и больше: «операций перейдут».</param>
    public static string Of(int count, string one, string few, string many) =>
        $"{count} {FormOf(count, one, few, many)}";

    /// <summary>
    /// Выбирает форму слова без числа.
    /// </summary>
    /// <param name="count">Количество.</param>
    /// <param name="one">Форма при одном.</param>
    /// <param name="few">Форма при двух-четырёх.</param>
    /// <param name="many">Форма при пяти и больше.</param>
    public static string FormOf(int count, string one, string few, string many)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(one);
        ArgumentException.ThrowIfNullOrWhiteSpace(few);
        ArgumentException.ThrowIfNullOrWhiteSpace(many);

        int hundreds = Math.Abs(count) % 100;
        int tens = hundreds % 10;

        // Одиннадцать и вокруг — исключение из правила последней цифры:
        // «11 операций», но «21 операция»
        return (hundreds, tens) switch
        {
            ( >= 11 and <= 14, _) => many,
            (_, 1) => one,
            (_, >= 2 and <= 4) => few,
            _ => many
        };
    }
}
