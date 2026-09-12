namespace Finance.Domain.Tests;

/// <summary>Как сообщается о нарушении доменного правила.</summary>
public sealed class DomainExceptionTests
{
    [Fact]
    public void Текст_не_собирается_когда_требование_не_нарушено()
    {
        // Проверки стоят на каждой сумме и каждой дате. Если перегрузку с ленивым
        // текстом убрать, тест упадёт, а иначе потеря нашлась бы только профайлером
        Counting value = new();

        DomainException.ThrowIf(false, Invariant.AmountIsPositive, $"значение {value}");

        Assert.Equal(0, value.Formatted);
    }

    [Fact]
    public void Текст_собирается_когда_требование_нарушено()
    {
        Counting value = new();

        DomainException error = Assert.Throws<DomainException>(
            () => DomainException.ThrowIf(true, Invariant.AmountIsPositive, $"значение {value}"));

        Assert.Equal(1, value.Formatted);
        Assert.Equal("значение посчитано", error.Message);
        Assert.Equal(Invariant.AmountIsPositive, error.Invariant);
    }

    [Fact]
    public void Обычная_строка_тоже_принимается()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => DomainException.ThrowIf(true, Invariant.TransferAccountsDiffer, "без подстановок"));

        Assert.Equal("без подстановок", error.Message);
    }

    /// <summary>Считает, сколько раз её подставили в текст.</summary>
    private sealed class Counting
    {
        public int Formatted { get; private set; }

        public override string ToString()
        {
            Formatted++;
            return "посчитано";
        }
    }
}
