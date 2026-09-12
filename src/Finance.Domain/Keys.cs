using System.Security.Cryptography;
using System.Text;

namespace Finance.Domain;

/// <summary>
/// Ключи сущностей. Их генерирует клиент, а не база: записи создаются офлайн,
/// и ключ обязан существовать до того, как о нём кто-либо узнает.
/// </summary>
public static class Keys
{
    private const int GuidSize = 16;

    /// <summary>
    /// Новый ключ сущности — UUIDv7. Версия выбрана не ради моды: ключ монотонен,
    /// поэтому новые строки ложатся в конец индекса, а не расщепляют его страницы,
    /// и он же разрывает ничью в сортировке ленты при совпавшем времени.
    /// </summary>
    public static Guid New() => Guid.CreateVersion7();

    /// <summary>
    /// Ключ категории стартового набора — UUIDv5 от пространства имён и устойчивого
    /// текстового ключа. Детерминированность обязательна: со случайными ключами
    /// установка на второе устройство создала бы дубликат всего набора.
    /// </summary>
    /// <param name="namespaceKey">Пространство имён из <c>data/preset.json</c>. Не меняется никогда.</param>
    /// <param name="name">Устойчивый текстовый ключ, например <c>food.groceries</c>.</param>
    public static Guid Derive(Guid namespaceKey, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // RFC 9562 требует сетевой порядок байтов. Раскладка .NET отличается для
        // первых трёх полей, и Guid.ToByteArray() без bigEndian даёт другой хеш
        byte[] input = new byte[GuidSize + Encoding.UTF8.GetByteCount(name)];
        namespaceKey.TryWriteBytes(input, bigEndian: true, out _);
        Encoding.UTF8.GetBytes(name, input.AsSpan(GuidSize));

        Span<byte> result = SHA1.HashData(input).AsSpan(0, GuidSize);
        result[6] = (byte)((result[6] & 0x0F) | 0x50); // версия 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // вариант RFC 9562

        return new Guid(result, bigEndian: true);
    }
}
