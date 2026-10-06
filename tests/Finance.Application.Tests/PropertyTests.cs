using System.Globalization;
using CsCheck;
using Finance.Application.Features.Report;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Свойства чистых функций на случайных входах: то, что обязано выполняться
/// для любого значения, а не для пяти придуманных. Примеры с объяснением
/// трудных случаев остаются в своих классах.
/// </summary>
public sealed class PropertyTests
{
    // Проверка чистой функции стоит микросекунды: десять тысяч входов укладываются
    // в доли секунды и находят то, что сотня пропускает
    private const int Iterations = 10_000;

    /// <summary>
    /// Наибольшая сумма в копейках, которую принимает домен.
    /// </summary>
    private static readonly long MaxKopecks = decimal.ToInt64(Money.Limit * 100);

    [Fact]
    public void Сумма_переживает_перевод_в_копейки_и_обратно()
    {
        MinorUnitsConverter converter = new();

        Gen.Long[-MaxKopecks, MaxKopecks].Sample(kopecks =>
        {
            decimal amount = kopecks / 100m;

            Assert.Equal(kopecks, (long)converter.ConvertToProvider(amount)!);
            Assert.Equal(amount, (decimal)converter.ConvertFromProvider(kopecks)!);
        }, iter: Iterations);
    }

    [Fact]
    public void Доля_копейки_не_округляется_молча()
    {
        MinorUnitsConverter converter = new();

        Gen.Select(Gen.Long[-MaxKopecks, MaxKopecks], Gen.Int[1, 9]).Sample((kopecks, tail) =>
        {
            decimal amount = ((kopecks * 10) + (kopecks < 0 ? -tail : tail)) / 1000m;

            Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider(amount));
        }, iter: Iterations);
    }

    [Fact]
    public void Набранное_число_читается_как_есть_при_любом_разделителе()
    {
        Gen.Select(Gen.Long[-MaxKopecks, MaxKopecks], Gen.OneOfConst('.', ','), Gen.OneOfConst('-', '−')).Sample(
            (kopecks, separator, minus) =>
            {
                decimal amount = kopecks / 100m;
                string typed = Typed(amount, separator, minus);

                Assert.True(AmountExpression.TryEvaluate(typed, out decimal result), typed);
                Assert.Equal(amount, result);
            }, iter: Iterations);
    }

    [Fact]
    public void Выражение_считается_слева_направо_как_на_калькуляторе()
    {
        Gen<(char Operation, decimal Operand)> step = Gen.Select(
            Gen.OneOfConst('+', '-', '*', '/', '×', '÷', '−'),
            Gen.Int[0, 1_000_000].Select(static kopecks => kopecks / 100m));

        Gen.Select(Gen.Int[-1_000_000, 1_000_000], step.Array[1, 6], Gen.OneOfConst(".", ","), Gen.OneOfConst("", " ", "  "))
            .Sample((firstKopecks, steps, separator, space) =>
            {
                decimal first = firstKopecks / 100m;
                string typed = Typed(first, separator[0], '-')
                               + string.Concat(steps.Select(s => $"{space}{s.Operation}{space}{Typed(s.Operand, separator[0], '-')}"));

                bool expectedDone = Calculator(first, steps, out decimal expected);
                bool done = AmountExpression.TryEvaluate(typed, out decimal result);

                Assert.Equal(expectedDone, done);
                Assert.Equal(expected, result);
            }, iter: Iterations);
    }

    [Fact]
    public void Месяц_отчёта_переживает_параметр_перехода()
    {
        Gen.Int[new DateOnly(2000, 1, 1).DayNumber, new DateOnly(2100, 12, 31).DayNumber].Sample(dayNumber =>
        {
            DateOnly day = DateOnly.FromDayNumber(dayNumber);
            ReportMonth month = ReportMonth.Of(day);

            Assert.True(ReportMonth.TryParse(month.ToString(), out ReportMonth parsed));
            Assert.Equal(month, parsed);
            Assert.InRange(day, month.First, month.Last);
            Assert.Equal(month, month.Next.Previous);
            Assert.Equal(month.Last.AddDays(1), month.Next.First);
        }, iter: Iterations);
    }

    [Fact]
    public void Ключ_позже_по_времени_идёт_позже_и_текстом()
    {
        // Лента сортируется по ключу в базе, где ключ — текст: ключ, заведённый
        // в следующую миллисекунду, обязан быть больше и как строка
        GuidToTextConverter converter = new();
        long from = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        long to = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        Gen.Select(Gen.Long[from, to], Gen.Long[1, 1_000_000_000]).Sample((earlier, gap) =>
        {
            Guid first = Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(earlier));
            Guid second = Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(earlier + gap));

            string firstText = (string)converter.ConvertToProvider(first)!;
            string secondText = (string)converter.ConvertToProvider(second)!;

            Assert.True(string.CompareOrdinal(firstText, secondText) < 0, $"{firstText} {secondText}");
        }, iter: Iterations);
    }

    [Fact]
    public void Ключи_в_памяти_упорядочены_как_текст_в_базе()
    {
        // Сортировка в памяти сравнивает Guid, база — текст: порядки обязаны совпадать,
        // иначе список, упорядоченный в памяти, разошёлся бы с лентой из базы
        GuidToTextConverter converter = new();

        Gen.Select(Gen.Guid, Gen.Guid).Sample((left, right) =>
        {
            string leftText = (string)converter.ConvertToProvider(left)!;
            string rightText = (string)converter.ConvertToProvider(right)!;

            Assert.Equal(Math.Sign(string.CompareOrdinal(leftText, rightText)), Math.Sign(left.CompareTo(right)));
        }, iter: Iterations);
    }

    /// <summary>
    /// Сумма так, как её набирают: с выбранным разделителем и, у отрицательной, минусом спереди.
    /// </summary>
    private static string Typed(decimal amount, char separator, char minus)
    {
        string digits = Math.Abs(amount).ToString(CultureInfo.InvariantCulture).Replace('.', separator);

        return amount < 0 ? $"{minus}{digits}" : digits;
    }

    /// <summary>
    /// Карманный калькулятор: действия по очереди, деление на ноль и переполнение —
    /// незаконченный ввод, итог округляется до копеек от нуля.
    /// </summary>
    private static bool Calculator(decimal first, (char Operation, decimal Operand)[] steps, out decimal result)
    {
        result = 0m;
        decimal value = first;

        try
        {
            foreach ((char operation, decimal operand) in steps)
            {
                if (operation is '/' or '÷' && operand == 0m)
                {
                    return false;
                }

                value = operation switch
                {
                    '+' => value + operand,
                    '-' or '−' => value - operand,
                    '*' or '×' => value * operand,
                    _ => value / operand
                };
            }

            result = decimal.Round(value, 2, MidpointRounding.AwayFromZero);

            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
