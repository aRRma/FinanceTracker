using System.Text;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Читатель формата protobuf без схемы: поле за полем, с пропуском ненужных. Его хватает, чтобы вынуть
/// из трассы нативного падения несколько полей, и библиотека ради этого не нужна.
/// </summary>
/// <remarks>
/// Испорченные данные — оборванное поле, длина за концом буфера — дают <see cref="InvalidDataException"/>,
/// а не выход за границу: трассу пишет система, и разбор не вправе ей доверять.
/// </remarks>
/// <param name="data">Сообщение целиком.</param>
internal ref struct ProtoReader(ReadOnlySpan<byte> data)
{
    private const int Varint = 0;
    private const int Fixed64 = 1;
    private const int LengthDelimited = 2;
    private const int Fixed32 = 5;

    private readonly ReadOnlySpan<byte> _data = data;
    private int _position;

    /// <summary>
    /// Читает номер и вид следующего поля.
    /// </summary>
    /// <param name="field">Номер поля.</param>
    /// <param name="wireType">Вид кодирования.</param>
    /// <returns><c>false</c>, если сообщение кончилось.</returns>
    internal bool TryReadTag(out int field, out int wireType)
    {
        if (_position >= _data.Length)
        {
            field = 0;
            wireType = 0;

            return false;
        }

        ulong tag = ReadVarint();
        field = (int)(tag >> 3);
        wireType = (int)(tag & 7);

        return field > 0 ? true : throw Malformed();
    }

    /// <summary>
    /// Читает число переменной длины.
    /// </summary>
    internal ulong ReadVarint()
    {
        ulong result = 0;

        for (int shift = 0; shift < 64; shift += 7)
        {
            if (_position >= _data.Length)
            {
                throw Malformed();
            }

            byte next = _data[_position++];
            result |= (ulong)(next & 0x7F) << shift;

            if ((next & 0x80) is 0)
            {
                return result;
            }
        }

        throw Malformed();
    }

    /// <summary>
    /// Читает поле с длиной: строку, байты или вложенное сообщение.
    /// </summary>
    internal ReadOnlySpan<byte> ReadBytes()
    {
        ulong length = ReadVarint();

        if (length > (ulong)(_data.Length - _position))
        {
            throw Malformed();
        }

        ReadOnlySpan<byte> bytes = _data.Slice(_position, (int)length);
        _position += (int)length;

        return bytes;
    }

    /// <summary>
    /// Читает строку UTF-8.
    /// </summary>
    internal string ReadString() => Encoding.UTF8.GetString(ReadBytes());

    /// <summary>
    /// Пропускает значение поля.
    /// </summary>
    /// <param name="wireType">Вид кодирования поля.</param>
    internal void Skip(int wireType)
    {
        switch (wireType)
        {
            case Varint:
                ReadVarint();
                break;
            case LengthDelimited:
                ReadBytes();
                break;
            case Fixed64:
                Advance(8);
                break;
            case Fixed32:
                Advance(4);
                break;
            default:
                throw Malformed();
        }
    }

    private void Advance(int count)
    {
        if (count > _data.Length - _position)
        {
            throw Malformed();
        }

        _position += count;
    }

    private static InvalidDataException Malformed() => new("protobuf");
}
