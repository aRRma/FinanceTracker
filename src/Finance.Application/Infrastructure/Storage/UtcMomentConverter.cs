using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Момент времени хранится текстом фиксированной длины в UTC. Поставщик SQLite
/// хранит <see cref="DateTimeOffset"/> текстом и сам, но сортировать по нему
/// отказывается: у разных строк могло бы быть разное смещение, и порядок текста
/// разошёлся бы с порядком моментов. Здесь смещение всегда нулевое, длина всегда
/// одна, и текст сортируется как время — а ленте сортировка по моменту записи нужна.
/// </summary>
internal sealed class UtcMomentConverter : ValueConverter<DateTimeOffset, string>
{
    /// <summary>
    /// Семь знаков долей секунды всегда: разная длина ломает сравнение текста.
    /// </summary>
    private const string Format = "yyyy-MM-dd HH:mm:ss.fffffff+00:00";

    public UtcMomentConverter()
        : base(
            moment => moment.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture),
            text => DateTimeOffset.ParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal))
    {
    }
}
