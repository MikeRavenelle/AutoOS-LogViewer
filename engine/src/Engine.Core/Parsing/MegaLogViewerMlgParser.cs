using System.Buffers.Binary;
using System.Text;
using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

public sealed class MegaLogViewerMlgParser : ILogParser
{
    public string Id => "mlg-binary";
    public string DisplayName => "MegaLogViewer / TunerStudio binary (.mlg)";

    private const double TimestampTickSeconds = 10e-6;
    private const int TimestampWrap = 65536;
    private const int V1FieldRecordLength = 55;
    private const int V2FieldRecordLength = 89;

    public bool CanParse(string path, ReadOnlySpan<char> preview)
        => preview.Length >= 5 && preview[..5].SequenceEqual("MLVLG");

    public ParsedLog Parse(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 22)
            throw new FormatException("File too short to contain an MLVLG header.");
        if (Encoding.ASCII.GetString(bytes, 0, 5) != "MLVLG")
            throw new FormatException("Not an MLVLG (.mlg) binary log file.");

        int pos = 6;
        int version = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(pos));
        pos += 2;
        bool isV2 = version >= 2;
        int fieldRecordLength = isV2 ? V2FieldRecordLength : V1FieldRecordLength;

        pos += 4;
        pos += isV2 ? 4 : 2;
        int dataBeginIndex = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(pos));
        pos += 4;
        int recordLength = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(pos));
        pos += 2;
        int numFields = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(pos));
        pos += 2;

        if (numFields <= 0)
            throw new FormatException("MLVLG header declares no Logger Fields.");

        var fields = new FieldDef[numFields];
        for (int i = 0; i < numFields; i++)
        {
            fields[i] = ParseFieldDef(bytes, pos);
            pos += fieldRecordLength;
        }

        var time = new List<double>();
        var samples = new List<float>[numFields];
        for (int i = 0; i < numFields; i++) samples[i] = [];

        pos = dataBeginIndex;
        double elapsed = 0;
        int? prevTick = null;
        while (pos < bytes.Length)
        {
            if (pos + 4 > bytes.Length) break;
            byte blockType = bytes[pos];
            int recordTick = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(pos + 2));
            pos += 4;

            if (blockType == 0)
            {
                if (pos + recordLength + 1 > bytes.Length) break;
                for (int i = 0; i < numFields; i++)
                {
                    samples[i].Add(ReadFieldValue(bytes, pos, fields[i]));
                    pos += fields[i].ByteSize;
                }
                pos += 1;

                int delta = prevTick is null ? 0 : recordTick - prevTick.Value;
                if (delta < 0) delta += TimestampWrap;
                elapsed += delta * TimestampTickSeconds;
                prevTick = recordTick;
                time.Add(elapsed);
            }
            else if (blockType == 1)
            {
                pos += 50;
            }
            else
            {
                break;
            }
        }

        var channels = new ChannelInfo[numFields];
        var data = new float[numFields][];
        for (int i = 0; i < numFields; i++)
        {
            channels[i] = new ChannelInfo(i, fields[i].Name, fields[i].Units);
            data[i] = [.. samples[i]];
        }

        return new ParsedLog
        {
            SourcePath = path,
            ParserId = Id,
            Metadata = new Dictionary<string, string>(),
            Time = [.. time],
            Channels = channels,
            Data = data,
        };
    }

    private readonly record struct FieldDef(string Name, string Units, int Type, float Scale, float Transform, int ByteSize);

    private static FieldDef ParseFieldDef(byte[] bytes, int pos)
    {
        int type = bytes[pos];
        string name = ReadFixedString(bytes, pos + 1, 34);
        string units = ReadFixedString(bytes, pos + 35, 10);

        if (type < 10)
        {
            float scale = BinaryPrimitives.ReadSingleBigEndian(bytes.AsSpan(pos + 46));
            float transform = BinaryPrimitives.ReadSingleBigEndian(bytes.AsSpan(pos + 50));
            return new FieldDef(name, units, type, scale, transform, ScalarByteSize(type));
        }
        return new FieldDef(name, units, type, 1f, 0f, ScalarByteSize(type));
    }

    private static int ScalarByteSize(int type) => type switch
    {
        0 or 1 or 10 => 1,
        2 or 3 or 11 => 2,
        4 or 5 or 12 => 4,
        7 => 4,
        6 => 8,
        _ => throw new FormatException($"Unknown Logger Field type {type}"),
    };

    private static float ReadFieldValue(byte[] bytes, int pos, FieldDef f)
    {
        var span = bytes.AsSpan(pos);
        float raw = f.Type switch
        {
            0 or 10 => bytes[pos],
            1 => (sbyte)bytes[pos],
            2 or 11 => BinaryPrimitives.ReadUInt16BigEndian(span),
            3 => BinaryPrimitives.ReadInt16BigEndian(span),
            4 or 12 => BinaryPrimitives.ReadUInt32BigEndian(span),
            5 => BinaryPrimitives.ReadInt32BigEndian(span),
            6 => BinaryPrimitives.ReadInt64BigEndian(span),
            7 => BinaryPrimitives.ReadSingleBigEndian(span),
            _ => throw new FormatException($"Unknown Logger Field type {f.Type}"),
        };
        return f.Type >= 10 ? raw : (raw + f.Transform) * f.Scale;
    }

    private static string ReadFixedString(byte[] bytes, int pos, int length)
        => Encoding.UTF8.GetString(bytes, pos, length).Replace("\0", "").Trim();
}
