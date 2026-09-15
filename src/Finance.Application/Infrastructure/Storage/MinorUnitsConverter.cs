using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Деньги хранятся целыми копейками в колонке <c>INTEGER</c>.
/// </summary>
/// <remarks>
/// Провайдер SQLite по умолчанию кладёт <see cref="decimal"/> в TEXT, а по тексту
/// не работают ни <c>SUM</c>, ни сравнение: баланс пришлось бы считать в памяти,
/// подняв всю ленту. Числа с плавающей точкой запрещены — они и есть та ошибка,
/// ради которой в домене заведён <c>Money</c>. Остаются целые копейки: предел суммы
/// 999 999 999 999,99 — это 99 999 999 999 999 копеек, до предела <see cref="long"/>
/// ещё пять порядков.
/// </remarks>
internal sealed class MinorUnitsConverter : ValueConverter<decimal, long>
{
    private const int MinorUnitsInMajor = 100;

    public MinorUnitsConverter()
        : base(amount => ToMinorUnits(amount),
               minor => minor / (decimal)MinorUnitsInMajor)
    {
    }

    /// <summary>
    /// Домен не выпускает наружу сумму точнее копейки, поэтому дробной части здесь
    /// быть не должно. Приведение всё же проверяет, а не отбрасывает: молча срезанная
    /// доля копейки разошлась бы с тем, что пользователь видел на экране.
    /// </summary>
    private static long ToMinorUnits(decimal amount)
    {
        decimal minor = amount * MinorUnitsInMajor;

        if (minor != decimal.Truncate(minor))
        {
            throw new InvalidOperationException($"Сумма {amount} точнее копейки и в базу не записывается");
        }

        // decimal.ToInt64 бросает на переполнении, приведение (long) — молча заворачивает
        return decimal.ToInt64(minor);
    }
}
