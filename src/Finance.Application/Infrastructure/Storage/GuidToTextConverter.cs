using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Ключ хранится текстом в каноническом виде, а не двоично. Раскладка байтов
/// <see cref="Guid"/> в .NET отличается от сетевого порядка: двоичное хранение
/// переставило бы старшие байты UUIDv7 и разом лишило бы и сортировку ленты
/// её последнего разрывателя ничьей, и индекс — локальности вставок.
/// </summary>
/// <remarks>
/// Регистр всегда нижний: SQLite сравнивает текст побайтово, и одна и та же
/// запись в разном регистре перестала бы находиться по ключу.
/// </remarks>
internal sealed class GuidToTextConverter : ValueConverter<Guid, string>
{
    public GuidToTextConverter()
        : base(key => key.ToString("D"), text => Guid.ParseExact(text, "D"))
    {
    }
}
