using Finance.Application.Infrastructure;

namespace Finance.Application.Tests;

/// <summary>Склонение счётных форм: подписи справочников и диалог удаления пишутся по-русски.</summary>
public sealed class PluralTests
{
    /// <summary>
    /// Форма выбирается по последней цифре, кроме второго десятка: «21 операция»,
    /// но «11 операций». Ошибка здесь не ломает данные, зато видна на каждом экране.
    /// </summary>
    [Theory]
    [InlineData(0, "0 операций")]
    [InlineData(1, "1 операция")]
    [InlineData(2, "2 операции")]
    [InlineData(4, "4 операции")]
    [InlineData(5, "5 операций")]
    [InlineData(11, "11 операций")]
    [InlineData(12, "12 операций")]
    [InlineData(14, "14 операций")]
    [InlineData(21, "21 операция")]
    [InlineData(22, "22 операции")]
    [InlineData(37, "37 операций")]
    [InlineData(101, "101 операция")]
    [InlineData(111, "111 операций")]
    public void Число_склоняет_слово_по_русским_правилам(int count, string expected) =>
        Assert.Equal(expected, Plural.Of(count, "операция", "операции", "операций"));

    /// <summary>Форма без числа: подпись «Категории» собирает две формы в одну строку.</summary>
    [Fact]
    public void Форма_берётся_и_без_числа() =>
        Assert.Equal("группы", Plural.FormOf(3, "группа", "группы", "групп"));

    /// <summary>Пустая форма — ошибка вызывающего кода, а не пустая подпись на экране.</summary>
    [Fact]
    public void Пустая_форма_отвергается() =>
        Assert.Throws<ArgumentException>(() => Plural.Of(1, "группа", " ", "групп"));
}
