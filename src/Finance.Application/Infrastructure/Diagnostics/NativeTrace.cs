using System.Globalization;
using System.Text;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Трасса нативного падения — то, что Android прикладывает к записи о нём, в protobuf по схеме
/// <c>tombstone.proto</c>. Из неё берутся только сигнал и стек упавшего потока.
/// </summary>
/// <remarks>
/// Остальное в трассе — регистры, куски памяти вокруг них, карты памяти, открытые файлы, журнал устройства,
/// сообщение о прерывании — бывает с данными пользователя: в памяти лежат строки из базы. Пользователь решил
/// хранить только стек. Номера полей — из схемы AOSP; поле, которого нет, просто пропускается.
/// </remarks>
internal static class NativeTrace
{
    /// <summary>
    /// Глубже кадры не пишутся: причина падения — в верхних.
    /// </summary>
    private const int MaxFrames = 64;

    // Tombstone
    private const int TombstoneTid = 6;
    private const int TombstoneSignal = 10;
    private const int TombstoneThreads = 16;

    // Signal
    private const int SignalNumber = 1;
    private const int SignalName = 2;
    private const int SignalCode = 3;
    private const int SignalCodeName = 4;

    // map<uint32, Thread>: запись словаря — сообщение с ключом и значением
    private const int EntryKey = 1;
    private const int EntryValue = 2;

    // Thread
    private const int ThreadName = 2;
    private const int ThreadBacktrace = 4;

    // BacktraceFrame
    private const int FrameRelativePc = 1;
    private const int FrameFunction = 4;
    private const int FrameFunctionOffset = 5;
    private const int FrameFile = 6;

    /// <summary>
    /// Описывает падение: сигнал, имя упавшего потока и его стек.
    /// </summary>
    /// <param name="trace">Трасса целиком.</param>
    /// <remarks>
    /// Испорченное место посреди трассы не стирает уже прочитанное: сигнал и первые кадры — самое ценное,
    /// и отчёт сохраняет их с пометкой, что дальше трасса не прочиталась.
    /// </remarks>
    internal static string Describe(ReadOnlySpan<byte> trace)
    {
        StringBuilder text = new();
        bool broken = false;
        int tid = 0;

        // Проходов два, и каждый ловит порчу сам: испорченное поле после потоков не должно
        // стоить стека, а испорченный поток — уже прочитанного сигнала
        try
        {
            ReadTombstone(trace, text, ref tid);
        }
        catch (InvalidDataException)
        {
            broken = true;
        }

        try
        {
            AppendThread(trace, tid, text);
        }
        catch (InvalidDataException)
        {
            broken = true;
        }

        if (broken)
        {
            text.Append(text.Length is 0 ? "native trace unreadable\n" : "native trace truncated\n");
        }

        return text.ToString();
    }

    private static void ReadTombstone(ReadOnlySpan<byte> trace, StringBuilder text, ref int tid)
    {
        ProtoReader reader = new(trace);

        while (reader.TryReadTag(out int field, out int wireType))
        {
            switch (field)
            {
                case TombstoneTid when wireType is 0:
                    tid = (int)reader.ReadVarint();
                    break;
                case TombstoneSignal when wireType is 2:
                    AppendSignal(reader.ReadBytes(), text);
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }
    }

    private static void AppendSignal(ReadOnlySpan<byte> signal, StringBuilder text)
    {
        ProtoReader reader = new(signal);
        long number = 0;
        long code = 0;
        string name = "";
        string codeName = "";

        while (reader.TryReadTag(out int field, out int wireType))
        {
            switch (field)
            {
                case SignalNumber when wireType is 0:
                    number = (long)reader.ReadVarint();
                    break;
                case SignalName when wireType is 2:
                    name = reader.ReadString();
                    break;
                case SignalCode when wireType is 0:
                    code = (long)reader.ReadVarint();
                    break;
                case SignalCodeName when wireType is 2:
                    codeName = reader.ReadString();
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        text.Append(CultureInfo.InvariantCulture, $"signal {number} ({name}), code {code} ({codeName})\n");
    }

    private static void AppendThread(ReadOnlySpan<byte> trace, int tid, StringBuilder text)
    {
        ProtoReader reader = new(trace);

        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field is not TombstoneThreads || wireType is not 2)
            {
                reader.Skip(wireType);
                continue;
            }

            ProtoReader entry = new(reader.ReadBytes());
            long key = -1;
            ReadOnlySpan<byte> thread = default;

            while (entry.TryReadTag(out int entryField, out int entryWire))
            {
                if (entryField is EntryKey && entryWire is 0)
                {
                    key = (long)entry.ReadVarint();
                }
                else if (entryField is EntryValue && entryWire is 2)
                {
                    thread = entry.ReadBytes();
                }
                else
                {
                    entry.Skip(entryWire);
                }
            }

            // Стек нужен только упавшего потока: остальные спят и о причине не говорят
            if (key == tid)
            {
                AppendBacktrace(thread, text);

                return;
            }
        }
    }

    private static void AppendBacktrace(ReadOnlySpan<byte> thread, StringBuilder text)
    {
        ProtoReader reader = new(thread);
        int frames = 0;

        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field is ThreadName && wireType is 2)
            {
                text.Append("thread ").Append(reader.ReadString()).Append('\n');
            }
            else if (field is ThreadBacktrace && wireType is 2 && frames < MaxFrames)
            {
                AppendFrame(reader.ReadBytes(), frames++, text);
            }
            else
            {
                reader.Skip(wireType);
            }
        }
    }

    private static void AppendFrame(ReadOnlySpan<byte> frame, int index, StringBuilder text)
    {
        ProtoReader reader = new(frame);
        ulong pc = 0;
        ulong offset = 0;
        string function = "";
        string file = "";

        while (reader.TryReadTag(out int field, out int wireType))
        {
            switch (field)
            {
                case FrameRelativePc when wireType is 0:
                    pc = reader.ReadVarint();
                    break;
                case FrameFunction when wireType is 2:
                    function = reader.ReadString();
                    break;
                case FrameFunctionOffset when wireType is 0:
                    offset = reader.ReadVarint();
                    break;
                case FrameFile when wireType is 2:
                    file = reader.ReadString();
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        // Вид строки — как у самой системы в журнале: так её узнают и ищут по ней
        text.Append(CultureInfo.InvariantCulture, $"#{index:D2} pc {pc:x16}  {file}");

        if (function.Length > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" ({function}+{offset})");
        }

        text.Append('\n');
    }
}
