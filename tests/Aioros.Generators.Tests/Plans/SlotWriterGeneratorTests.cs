using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Generators.Tests.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Generators.Tests.Plans;

public sealed class SlotWriterGeneratorTests
{
    private const string Library = """
        using Aioros.Runtime.Rendering;

        [assembly: SlotWriter<Charts.LineWriter>]
        [assembly: SlotWriter<Charts.Outer.BarWriter>]

        namespace Charts;

        public sealed class LineWriter : ISlotWriter
        {
            public static SlotWrite Create(string argument, MemberLookup members) =>
                (value, output) => System.Buffers.BuffersExtensions.Write(output, System.Text.Encoding.UTF8.GetBytes("line:" + argument + ":" + value));
        }

        public static class Outer
        {
            public sealed class BarWriter : ISlotWriter
            {
                public static SlotWrite Create(string argument, MemberLookup members) => (_, _) => { };
            }
        }
        """;

    private const string App = """
        using Aioros.Runtime.Rendering;

        [assembly: SlotWriter<App.Views.BadgeWriter>]

        namespace App.Views;

        public sealed class BadgeWriter : ISlotWriter
        {
            public static SlotWrite Create(string argument, MemberLookup members) => (_, _) => { };
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-112")]
    public void DeclaredSlotWritersOfTheAppAndItsLibrariesAreRegisteredByName()
    {
        var assembly = new GeneratorHarness().Library("Charts", Library).Source(App).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var all = (ImmutableArray<SlotWriterEntry>)assembly.GetType("Aioros.Generated.AiorosSlotWriters")!.GetProperty("All")!.GetValue(null)!;
        var output = new ArrayBufferWriter<byte>();

        Render(all.Single(entry => entry.Name == "Charts.LineWriter").Create("a", (_, _) => _ => null), 7, output);

        Assert.Equal(["App.Views.BadgeWriter", "Charts.LineWriter", "Charts.Outer+BarWriter"], all.Select(entry => entry.Name));
        Assert.Equal("line:a:7", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [Fact]
    [Trait("AC", "GENERATOR-113")]
    public void DeclaredSlotRenderersAreRegisteredBesideWriters()
    {
        var assembly = new GeneratorHarness().Library("Charts", Library).Library("Pies", Renderers).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var all = (ImmutableArray<SlotWriterEntry>)assembly.GetType("Aioros.Generated.AiorosSlotWriters")!.GetProperty("All")!.GetValue(null)!;
        var rendered = new ArrayBufferWriter<byte>();
        var written = new ArrayBufferWriter<byte>();

        Render(all.Single(entry => entry.Name == "Charts.PieRenderer").Create("b", (_, _) => _ => null), 3, rendered);
        Render(all.Single(entry => entry.Name == "Charts.LineWriter").Create("c", (_, _) => _ => null), 4, written);

        Assert.Equal(["Charts.LineWriter", "Charts.Outer+BarWriter", "Charts.PieRenderer"], all.Select(entry => entry.Name));
        Assert.Equal("pie:b:3", Encoding.UTF8.GetString(rendered.WrittenSpan));
        Assert.Equal("line:c:4", Encoding.UTF8.GetString(written.WrittenSpan));
    }

    [Fact]
    [Trait("AC", "GENERATOR-114")]
    public void RendererEntriesCarryTheirDecoders()
    {
        var assembly = new GeneratorHarness().Library("Charts", Library).Library("Pies", Renderers).Library("Brushes", Decoders).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var all = (ImmutableArray<SlotWriterEntry>)assembly.GetType("Aioros.Generated.AiorosSlotWriters")!.GetProperty("All")!.GetValue(null)!;
        var decoded = new string?[2];
        var refused = new string?[2];

        var slid = all.Single(entry => entry.Name == "Charts.BrushRenderer").Decode!("slide", "4", new object?[] { 1, 3 }, decoded);
        var pie = all.Single(entry => entry.Name == "Charts.PieRenderer").Decode!("slide", "4", new object?[] { 1, 3 }, refused);

        Assert.True(slid);
        Assert.Equal(["4", "6"], decoded);
        Assert.False(pie);
        Assert.Null(all.Single(entry => entry.Name == "Charts.LineWriter").Decode);
    }

    private const string Decoders = """
        [assembly: Aioros.Runtime.Rendering.SlotRenderer<Charts.BrushRenderer>]

        namespace Charts
        {
            public sealed class BrushRenderer : Aioros.Runtime.Rendering.ISlotRenderer
            {
                public static Aioros.Runtime.Rendering.SlotRender Create(string argument, Aioros.Runtime.Rendering.MemberLookup members) =>
                    (ref Aioros.Runtime.Rendering.SlotOutput output) => { };

                public static bool Decode(string key, string posted, System.ReadOnlySpan<object> current, System.Span<string> decoded)
                {
                    var start = int.Parse(posted);
                    decoded[0] = posted;
                    decoded[1] = (start + (int)current[1] - (int)current[0]).ToString();
                    return true;
                }
            }
        }
        """;

    private const string Renderers = """
        [assembly: Aioros.Runtime.Rendering.SlotRenderer<Charts.PieRenderer>]

        namespace Charts
        {
            public sealed class PieRenderer : Aioros.Runtime.Rendering.ISlotRenderer
            {
                public static Aioros.Runtime.Rendering.SlotRender Create(string argument, Aioros.Runtime.Rendering.MemberLookup members) =>
                    (ref Aioros.Runtime.Rendering.SlotOutput output) => System.Buffers.BuffersExtensions.Write(output.Buffer, System.Text.Encoding.UTF8.GetBytes("pie:" + argument + ":" + output.Value));
            }
        }
        """;

    private static void Render(SlotRender render, object value, IBufferWriter<byte> output)
    {
        var slot = new SlotOutput(new HtmlOutput(output), value);
        render(ref slot);
    }
}
