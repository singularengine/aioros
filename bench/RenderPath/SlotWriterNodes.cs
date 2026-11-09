using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Runtime.Rendering;
using BenchmarkDotNet.Attributes;

public sealed record Bar(int Value);

public sealed record Barred(ImmutableArray<Bar> Bars, string Color);

[MemoryDiagnoser]
public class SlotWriterNodes
{
    private static readonly MemberLookup Members = static (_, _) => static bar => ((Bar)bar).Value;

    private readonly ArrayBufferWriter<byte> _buffer = new(1 << 16);
    private Barred _model = null!;
    private SlotWrite _direct = null!;
    private WriterNode _single = null!;
    private WriterNode _parts = null!;

    [Params(10, 200)]
    public int Items { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _model = new Barred([.. Enumerable.Range(1, Items).Select(value => new Bar(value))], "red");
        _direct = BarsWriter.Create("w", Members);
        _single = new WriterNode(static model => ((Barred)model!).Bars, SlotWriters.Rendering<BarsWriter>()("w", Members));
        _parts = new WriterNode(static model => ((Barred)model!).Bars, BarsRenderer.Create("w", Members))
        {
            Parts =
            [
                new WriterPart("color") { Get = static model => ((Barred)model!).Color },
                new WriterPart("start") { Get = static _ => 0 },
                new WriterPart("end") { Get = static model => ((Barred)model!).Bars.Length },
            ],
        };
    }

    [Benchmark(Baseline = true, Description = "writer delegate called directly (the former node's work)")]
    public int Direct()
    {
        _buffer.ResetWrittenCount();
        _direct(_model.Bars, _buffer);
        return _buffer.WrittenCount;
    }

    [Benchmark(Description = "single-value writer through its node")]
    public int Single()
    {
        _buffer.ResetWrittenCount();
        var output = new HtmlOutput(_buffer);
        _single.Write(ref output, new RenderScope(_model, []));
        return _buffer.WrittenCount;
    }

    [Benchmark(Description = "renderer reading three bound parts")]
    public int Parts()
    {
        _buffer.ResetWrittenCount();
        var output = new HtmlOutput(_buffer);
        _parts.Write(ref output, new RenderScope(_model, []));
        return _buffer.WrittenCount;
    }

    public sealed class BarsWriter : ISlotWriter
    {
        public static SlotWrite Create(string argument, MemberLookup members) => (value, output) =>
        {
            output.Write("<svg>"u8);
            foreach (var bar in (ImmutableArray<Bar>)value!)
            {
                Rect(output, members(typeof(Bar), nameof(Bar.Value))(bar));
            }

            output.Write("</svg>"u8);
        };

        public static void Rect(IBufferWriter<byte> output, object? height)
        {
            output.Write("<rect height=\""u8);
            var span = output.GetSpan(11);
            ((int)height!).TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            output.Advance(written);
            output.Write("\"/>"u8);
        }
    }

    public sealed class BarsRenderer : ISlotRenderer
    {
        public static SlotRender Create(string argument, MemberLookup members) => (ref SlotOutput output) =>
        {
            var color = output.TryRead("color", out var bound) ? (string)bound! : argument;
            var start = output.TryRead("start", out var from) ? (int)from! : 0;
            var end = output.TryRead("end", out var to) ? (int)to! : 0;
            output.Buffer.Write("<svg fill=\""u8);
            output.Buffer.Advance(Encoding.UTF8.GetBytes(color, output.Buffer.GetSpan(Encoding.UTF8.GetMaxByteCount(color.Length))));
            output.Buffer.Write("\">"u8);
            var index = 0;
            foreach (var bar in (ImmutableArray<Bar>)output.Value!)
            {
                if (index++ >= start && index <= end)
                {
                    BarsWriter.Rect(output.Buffer, members(typeof(Bar), nameof(Bar.Value))(bar));
                }
            }

            output.Buffer.Write("</svg>"u8);
        };
    }
}
