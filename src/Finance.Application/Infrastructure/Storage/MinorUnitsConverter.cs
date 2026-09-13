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
        // Умножение точное: домен не выпускает наружу сумму точнее копейки
        // (Money.Create считает записанные знаки), поэтому дробной части здесь нет
        : base(amount => (long)(amount * MinorUnitsInMajor),
               minor => minor / (decimal)MinorUnitsInMajor)
    {
    }
}
