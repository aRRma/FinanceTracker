using System.Text;

namespace Finance.Application.Tests;

/// <summary>
/// Запись protobuf для тестов: из полей собирается образец трассы нативного падения. Настоящий образец
/// с эмулятора в репозиторий не кладётся — в нём куски памяти и список открытых файлов.
/// </summary>
internal static class Proto
{
    public static byte[] Varint(int field, ulong value) => [.. Tag(field, 0), .. Encode(value)];

    public static byte[] String(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value));

    public static byte[] Message(int field, params byte[][] parts) => Bytes(field, [.. parts.SelectMany(static part => part)]);

    public static byte[] Fixed64(int field, ulong value) => [.. Tag(field, 1), .. BitConverter.GetBytes(value)];

    public static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(static part => part)];

    private static byte[] Bytes(int field, byte[] value) => [.. Tag(field, 2), .. Encode((ulong)value.Length), .. value];

    private static byte[] Tag(int field, int wireType) => Encode(((ulong)(uint)field << 3) | (uint)wireType);

    private static byte[] Encode(ulong value)
    {
        List<byte> bytes = [];

        do
        {
            byte next = (byte)(value & 0x7F);
            value >>= 7;
            bytes.Add(value is 0 ? next : (byte)(next | 0x80));
        }
        while (value is not 0);

        return [.. bytes];
    }
}
