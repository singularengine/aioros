using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class WriterNodeTests
{
    [Fact]
    [Trait("AC", "RENDERING-20")]
    public void ARegisteredWriterWritesEachRenderFromTheBoundValueAfterARoundTrip()
    {
        MemberAccessors.Register(
        [
            new MemberAccessor(typeof(Chart), nameof(Chart.Points), static model => ((Chart)model).Points),
            new MemberAccessor(typeof(Point), nameof(Point.Value), static point => ((Point)point).Value),
        ]);
        SlotWriters.Register([new SlotWriterEntry(typeof(BarsWriter).FullName!, SlotWriters.Rendering<BarsWriter>())]);
        var resolver = new PlanSlotResolver();
        var source = SlotSpec.Bound(nameof(Chart.Points));
        var spec = SlotSpecOf("w=4");
        ImmutableArray<IPlanNode> body =
        [
            new LiteralNode("<div>"),
            new WriterNode(resolver.Getter(source), resolver.Writer(spec)) { Source = source, WriterSpec = spec },
            new LiteralNode("</div>"),
        ];
        var write = PlanSerializer.TryWrite(body);
        var created = BarsWriter.Created;

        var read = PlanSerializer.Read(write.Json!, resolver);

        Assert.Null(write.Failure);
        Assert.Equal(created + 1, BarsWriter.Created);
        Assert.Equal("<div><svg data-w=\"w=4\"><rect height=\"3\"/><rect height=\"5\"/></svg></div>", Render(read, new Chart([new Point(3), new Point(5)])));
        Assert.Equal("<div><svg data-w=\"w=4\"><rect height=\"7\"/></svg></div>", Render(read, new Chart([new Point(7)])));
        Assert.Equal(Render(body, new Chart([new Point(7)])), Render(read, new Chart([new Point(7)])));
        Assert.Equal(created + 1, BarsWriter.Created);
    }

    [Fact]
    [Trait("AC", "RENDERING-21")]
    public void AWriterWithNoEntryFailsToLoadNamingIt()
    {
        var source = SlotSpec.Bound(nameof(Chart.Points));
        var spec = SlotWriters.Spec("Missing.Writer", "{}");
        var write = PlanSerializer.TryWrite([new WriterNode(_ => null, (ref SlotOutput _) => { }) { Source = source, WriterSpec = spec }]);

        var error = Assert.Throws<JsonException>(() => PlanSerializer.Read(write.Json!, new PlanSlotResolver()));

        Assert.Contains("'Missing.Writer'", error.Message, StringComparison.Ordinal);
        Assert.Contains("SlotWriterAttribute<T>", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RENDERING-22")]
    public async Task PartsOfARendererAreReadPostedAndRunAfterARoundTripAsync()
    {
        MemberAccessors.Register(
        [
            new MemberAccessor(typeof(Plot), nameof(Plot.Points), static model => ((Plot)model).Points),
            new MemberAccessor(typeof(Plot), nameof(Plot.Color), static model => ((Plot)model).Color),
            new MemberAccessor(typeof(Plot), nameof(Plot.From), static model => ((Plot)model).From) { Set = static (model, value) => ((Plot)model).From = (int)value!, ValueType = static () => typeof(int) },
            new MemberAccessor(typeof(Plot), nameof(Plot.Pick), static model => ((Plot)model).Pick),
            new MemberAccessor(typeof(Point), nameof(Point.Value), static point => ((Point)point).Value),
        ]);
        SlotWriters.Register([new SlotWriterEntry(typeof(PlotRenderer).FullName!, PlotRenderer.Create)]);
        PlanCommands.Register([new CommandShape(typeof(Picking), static (command, parameter) =>
        {
            command.Execute(parameter);
            return ValueTask.CompletedTask;
        }) { Exact = true }]);
        var resolver = new PlanSlotResolver();
        var source = SlotSpec.Bound(nameof(Plot.Points));
        var color = SlotSpec.Bound(nameof(Plot.Color));
        var from = SlotSpec.Bound(nameof(Plot.From));
        var spec = SlotWriters.Spec(typeof(PlotRenderer).FullName!, "blue");
        ImmutableArray<WriterPart> parts =
        [
            new("color") { Read = color, Get = resolver.Getter(color) },
            new("from") { Read = from, Get = resolver.Getter(from), IsField = true, Field = new SlotSpec("number", nameof(Plot.From)) { Values = ["0", "9", "clip"] } },
            new("pick") { IsCommand = true, Command = new SlotSpec("command", nameof(Plot.Pick)) },
        ];
        ImmutableArray<IPlanNode> body = [new WriterNode(resolver.Getter(source), resolver.Writer(spec)) { Source = source, WriterSpec = spec, Parts = parts }];
        var read = PlanSerializer.Read(PlanSerializer.TryWrite(body).Json!, resolver);
        var model = new Plot([new Point(3), new Point(5)], "red", 1);

        var html = Render(read, model);
        var original = Render(body, model);
        var fields = PlanForms.Fields(new RenderPlan([], read, []), [model]).Fields;
        var bound = await PlanForms.ApplyAsync(new RenderPlan([], read, []), [model], Form(("_chg", "0"), ("f0", "12")), resolver);
        var ran = await PlanForms.ApplyAsync(new RenderPlan([], read, []), [model], Form(("f0", "9"), ("_btn", "1|1")), resolver);

        Assert.Equal("<svg fill=\"red\" data-none=\"False\"><input type=\"range\" value=\"1\" name=\"f0\" data-change><button type=\"submit\" name=\"_btn\" value=\"0|0\">3</button><button type=\"submit\" name=\"_btn\" value=\"1|1\">5</button></svg>", html);
        Assert.Equal(original, html);
        Assert.Equal(["number:From", "command:Pick:0", "command:Pick:1"], fields.Select(field => $"{field.Field!.Kind}:{field.Field.Path}{(field.Field.Kind == "command" ? ":" + field.Field.Values[0] : string.Empty)}"));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (bound, ran));
        Assert.Equal(9, model.From);
        Assert.Equal(["1"], model.Picked);
    }

    [Fact]
    [Trait("AC", "RENDERING-23")]
    public void AFieldOrCommandUnderAnUndeclaredKeyFailsTheRenderNamingIt()
    {
        var field = new WriterNode(WriterNode.NoValue, static (ref SlotOutput output) => output.WriteField("start")) { Parts = [new WriterPart("start") { IsCommand = true }] };
        var command = new WriterNode(WriterNode.NoValue, static (ref SlotOutput output) => output.WriteCommand("open", "1")) { Parts = [new WriterPart("open") { IsField = true }] };

        var unfielded = Assert.Throws<InvalidOperationException>(() => Render([field], new Chart([])));
        var uncommanded = Assert.Throws<InvalidOperationException>(() => Render([command], new Chart([])));

        Assert.Contains("'start'", unfielded.Message, StringComparison.Ordinal);
        Assert.Contains("'open'", uncommanded.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RENDERING-24")]
    public async Task ChoicesAndCompositesAreWrittenAndPostedAfterARoundTripAsync()
    {
        MemberAccessors.Register(
        [
            new MemberAccessor(typeof(Window), nameof(Window.Mode), static model => ((Window)model).Mode) { Set = static (model, value) => ((Window)model).Mode = (string)value!, ValueType = static () => typeof(string) },
            new MemberAccessor(typeof(Window), nameof(Window.Start), static model => ((Window)model).Start) { Set = static (model, value) => ((Window)model).Start = (int)value!, ValueType = static () => typeof(int) },
            new MemberAccessor(typeof(Window), nameof(Window.End), static model => ((Window)model).End) { Set = static (model, value) => ((Window)model).End = (int)value!, ValueType = static () => typeof(int) },
        ]);
        SlotWriters.Register([new SlotWriterEntry(typeof(WindowRenderer).FullName!, WindowRenderer.Create) { Decode = SlotWriters.Decoder<WindowRenderer>() }]);
        var resolver = new PlanSlotResolver();
        var mode = SlotSpec.Bound(nameof(Window.Mode));
        SlotSpec start = new("number", nameof(Window.Start)) { Values = ["0", "9", "clip"] };
        SlotSpec end = new("number", nameof(Window.End)) { Values = ["0", "9", "clip"] };
        var spec = SlotWriters.Spec(typeof(WindowRenderer).FullName!, string.Empty);
        ImmutableArray<WriterPart> parts =
        [
            new("mode") { Read = mode, Get = resolver.Getter(mode), IsField = true, IsChoice = true, Field = new SlotSpec("choice", nameof(Window.Mode)) },
            new("start") { IsField = true, Field = start },
            new("end") { IsField = true, Field = end },
            new("slide") { IsField = true, Field = CompositeFields.Spec(typeof(WindowRenderer).FullName!, "slide", [start, end]) },
        ];
        ImmutableArray<IPlanNode> body = [new WriterNode(WriterNode.NoValue, resolver.Writer(spec)) { WriterSpec = spec, Parts = parts }];
        var read = PlanSerializer.Read(PlanSerializer.TryWrite(body).Json!, resolver);
        var plan = new RenderPlan([], read, []);
        var model = new Window { Mode = "a", Start = 1, End = 3 };

        var html = Render(read, model);
        var fields = PlanForms.Fields(plan, [model]).Fields;
        await PlanForms.ApplyAsync(plan, [model], Form(("_chg", "0"), ("f0", "b")), resolver);
        var chosen = model.Mode;
        await PlanForms.ApplyAsync(plan, [model], Form(("_chg", "0"), ("f0", "z")), resolver);
        await PlanForms.ApplyAsync(plan, [model], Form(("_chg", "1"), ("f1", "8")), resolver);
        var misnamed = Assert.Throws<InvalidOperationException>(() => Render([new WriterNode(WriterNode.NoValue, static (ref SlotOutput output) => output.WriteFieldOption("start", "1")) { Parts = [new WriterPart("start") { IsField = true }] }], model));
        var plain = Assert.Throws<InvalidOperationException>(() => Render([new WriterNode(WriterNode.NoValue, static (ref SlotOutput output) => output.WriteField("mode")) { Parts = [new WriterPart("mode") { IsField = true, IsChoice = true }] }], model));

        Assert.Equal("<input type=\"radio\" name=\"f0\" value=\"a\" data-change checked><input type=\"radio\" name=\"f0\" value=\"b\" data-change><input type=\"range\" name=\"f1\" data-change>", html);
        Assert.Equal(Render(body, new Window { Mode = "a", Start = 1, End = 3 }), html);
        Assert.Equal(["choice:a,b", "composite:"], fields.Select(field => $"{field.Field!.Kind}:{(field.Options.IsDefault ? string.Empty : string.Join(',', field.Options))}"));
        Assert.Equal(["number:Start", "number:End"], CompositeFields.Members(fields[1].Field!).Select(member => $"{member.Kind}:{member.Path}"));
        Assert.Equal("b", chosen);
        Assert.Equal(("b", 8, 9), (model.Mode, model.Start, model.End));
        Assert.Contains("'start'", misnamed.Message, StringComparison.Ordinal);
        Assert.Contains("'mode'", plain.Message, StringComparison.Ordinal);
    }

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value);

    private static SlotSpec SlotSpecOf(string argument) => SlotWriters.Spec(typeof(BarsWriter).FullName!, argument);

    private static string Render(ImmutableArray<IPlanNode> body, object model)
    {
        var writer = new ArrayBufferWriter<byte>();
        new RenderPlan([], body, []).Render(writer, [model]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private sealed record Chart(ImmutableArray<Point> Points);

    private sealed record Point(int Value);

    private sealed class Plot(ImmutableArray<Point> points, string color, int from)
    {
        public ImmutableArray<Point> Points { get; } = points;

        public string Color { get; } = color;

        public int From { get; set; } = from;

        public List<string> Picked { get; } = [];

        public ICommand Pick => new Picking(Picked);
    }

    private sealed class Picking(List<string> picked) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => picked.Add((string)parameter!);
    }

    private sealed class PlotRenderer : ISlotRenderer
    {
        public static SlotRender Create(string argument, MemberLookup members) => (ref SlotOutput output) =>
        {
            var color = output.TryRead("color", out var bound) ? (string)bound! : argument;
            var none = output.TryRead("size", out _);
            output.Buffer.Write(Encoding.UTF8.GetBytes($"<svg fill=\"{color}\" data-none=\"{none}\"><input type=\"range\" value=\"{(output.TryRead("from", out var from) ? from : 0)}\""));
            output.WriteField("from");
            output.Buffer.Write(">"u8);
            var index = 0;
            foreach (var item in (System.Collections.IEnumerable)output.Value!)
            {
                output.Buffer.Write("<button"u8);
                output.WriteCommand("pick", (index++).ToString(CultureInfo.InvariantCulture));
                output.Buffer.Write(Encoding.UTF8.GetBytes($">{members(item.GetType(), nameof(Point.Value))(item)}</button>"));
            }

            output.Buffer.Write("</svg>"u8);
        };
    }

    private sealed class Window
    {
        public string Mode { get; set; } = string.Empty;

        public int Start { get; set; }

        public int End { get; set; }
    }

    private sealed class WindowRenderer : ISlotRenderer
    {
        public static SlotRender Create(string argument, MemberLookup members) => (ref SlotOutput output) =>
        {
            var mode = output.TryRead("mode", out var bound) ? bound as string : null;
            foreach (var option in (string[])["a", "b"])
            {
                output.Buffer.Write("<input type=\"radio\""u8);
                output.WriteFieldOption("mode", option);
                output.Buffer.Write(option == mode ? " checked>"u8 : ">"u8);
            }

            output.Buffer.Write("<input type=\"range\""u8);
            output.WriteField("slide");
            output.Buffer.Write(">"u8);
        };

        public static bool Decode(string key, string posted, ReadOnlySpan<object?> current, Span<string?> decoded)
        {
            var start = int.Parse(posted, CultureInfo.InvariantCulture);
            decoded[0] = posted;
            decoded[1] = (start + (int)current[1]! - (int)current[0]!).ToString(CultureInfo.InvariantCulture);
            return true;
        }
    }

    private sealed class BarsWriter : ISlotWriter
    {
        private static int _created;

        public static int Created => Volatile.Read(ref _created);

        public static SlotWrite Create(string argument, MemberLookup members)
        {
            Interlocked.Increment(ref _created);
            var open = Encoding.UTF8.GetBytes($"<svg data-w=\"{argument}\">");
            return (value, output) =>
            {
                output.Write(open);
                foreach (var item in (System.Collections.IEnumerable)value!)
                {
                    var height = members(item.GetType(), nameof(Point.Value))(item);
                    output.Write(Encoding.UTF8.GetBytes($"<rect height=\"{Convert.ToString(height, CultureInfo.InvariantCulture)}\"/>"));
                }

                output.Write("</svg>"u8);
            };
        }
    }
}
