using System.Buffers.Binary;
using System.Text;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class MegaLogViewerMlgParserTests
{
    private static byte[] BuildFile(
        (string Name, string Units, int Type, float Scale, float Transform)[] fieldDefs,
        (int Timestamp, float[] Values)[] records)
    {
        const int headerLength = 22;
        const int fieldRecordLength = 55;
        int fieldsEnd = headerLength + fieldDefs.Length * fieldRecordLength;
        int dataBeginIndex = fieldsEnd;
        int recordLength = fieldDefs.Sum(f => ScalarByteSize(f.Type));

        using var ms = new MemoryStream();
        void WriteBE16(int v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, (short)v); ms.Write(b); }
        void WriteBE32(int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); ms.Write(b); }
        void WriteBEFloat(float v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteSingleBigEndian(b, v); ms.Write(b); }
        void WriteFixedString(string s, int len)
        {
            var bytes = new byte[len];
            Encoding.ASCII.GetBytes(s).AsSpan(0, Math.Min(s.Length, len)).CopyTo(bytes);
            ms.Write(bytes);
        }

        ms.Write(Encoding.ASCII.GetBytes("MLVLG\0"));
        WriteBE16(1);
        WriteBE32(0);
        WriteBE16(0);
        WriteBE32(dataBeginIndex);
        WriteBE16(recordLength);
        WriteBE16(fieldDefs.Length);

        foreach (var f in fieldDefs)
        {
            ms.WriteByte((byte)f.Type);
            WriteFixedString(f.Name, 34);
            WriteFixedString(f.Units, 10);
            ms.WriteByte(0);
            WriteBEFloat(f.Scale);
            WriteBEFloat(f.Transform);
            ms.WriteByte(0);
        }

        foreach (var (timestamp, values) in records)
        {
            ms.WriteByte(0);
            ms.WriteByte(0);
            WriteBE16(timestamp);
            for (int i = 0; i < fieldDefs.Length; i++)
            {
                WriteScalar(ms, fieldDefs[i].Type, values[i], fieldDefs[i].Scale, fieldDefs[i].Transform);
            }
            ms.WriteByte(0);
        }

        return ms.ToArray();
    }

    private static void WriteScalar(MemoryStream ms, int type, float displayValue, float scale, float transform)
    {
        double raw = displayValue / scale - transform;
        Span<byte> b = stackalloc byte[8];
        switch (type)
        {
            case 2: BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)raw); ms.Write(b[..2]); break;
            case 3: BinaryPrimitives.WriteInt16BigEndian(b, (short)raw); ms.Write(b[..2]); break;
            case 7: BinaryPrimitives.WriteSingleBigEndian(b, (float)raw); ms.Write(b[..4]); break;
            default: throw new NotSupportedException($"test helper doesn't cover type {type}");
        }
    }

    private static int ScalarByteSize(int type) => type switch { 2 or 3 => 2, 7 => 4, _ => throw new NotSupportedException() };

    private static string TempFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"olv-mlg-{Guid.NewGuid():N}.mlg");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void CanParse_DetectsMagicBytes()
    {
        var parser = new MegaLogViewerMlgParser();
        Assert.True(parser.CanParse("x.mlg", "MLVLG\0anything after this is binary garbage".AsSpan()));
        Assert.False(parser.CanParse("x.csv", "Time (s),RPM\n0,1000".AsSpan()));
    }

    [Fact]
    public void Parse_ScalarFields_AppliesScaleAndTransform()
    {
        var bytes = BuildFile(
            fieldDefs:
            [
                ("RPM", "rpm", 2, 1f, 0f),
                ("Boost", "psi", 7, 1f, -14.7f),
            ],
            records:
            [
                (0, [4000f, 20.3f]),
                (100, [4200f, 21.0f]),
            ]);
        var path = TempFile(bytes);
        try
        {
            var log = new MegaLogViewerMlgParser().Parse(path);

            Assert.Equal(2, log.Channels.Length);
            Assert.Equal("RPM", log.Channels[0].Name);
            Assert.Equal("rpm", log.Channels[0].Unit);
            Assert.Equal("Boost", log.Channels[1].Name);
            Assert.Equal(2, log.SampleCount);
            Assert.Equal(4000f, log.Data[0][0], 2);
            Assert.Equal(4200f, log.Data[0][1], 2);
            Assert.Equal(20.3f, log.Data[1][0], 2);
            Assert.Equal(21.0f, log.Data[1][1], 2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_TimestampTicks_AccumulateAsSeconds()
    {
        var bytes = BuildFile(
            fieldDefs: [("RPM", "rpm", 2, 1f, 0f)],
            records:
            [
                (0, [1000f]),
                (1000, [1001f]),
                (2000, [1002f]),
            ]);
        var path = TempFile(bytes);
        try
        {
            var log = new MegaLogViewerMlgParser().Parse(path);

            Assert.Equal(0.0, log.Time[0], 6);
            Assert.Equal(0.01, log.Time[1], 6);
            Assert.Equal(0.02, log.Time[2], 6);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_TimestampWraparound_ReconstructsElapsedTime()
    {
        var bytes = BuildFile(
            fieldDefs: [("RPM", "rpm", 2, 1f, 0f)],
            records:
            [
                (65000, [1000f]),
                (500, [1001f]),
            ]);
        var path = TempFile(bytes);
        try
        {
            var log = new MegaLogViewerMlgParser().Parse(path);

            Assert.Equal(0.0, log.Time[0], 6);
            double expectedDeltaSeconds = 1036 * 10e-6;
            Assert.Equal(expectedDeltaSeconds, log.Time[1], 6);
            Assert.True(log.Time[1] > log.Time[0], "time must move forward across a wrap, not backward");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_NotAnMlgFile_Throws()
    {
        var path = TempFile(Encoding.ASCII.GetBytes("Time (s),RPM\n0,1000\n"));
        try
        {
            Assert.Throws<FormatException>(() => new MegaLogViewerMlgParser().Parse(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
