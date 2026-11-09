using System.Collections.Immutable;
using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class PlanAllocationTests
{
    [Fact]
    [Trait("AC", "RENDERING-25")]
    public void APlanBuildsItsTemplateOnlyWhenItIsRead()
    {
        var text = new string('x', 120_000);
        ImmutableArray<IPlanNode> body =
        [
            new LiteralNode("<main>" + text), new ValueNode(scope => scope), new LiteralNode(text),
            new RepeatNode(_ => Array.Empty<object>(), [new LiteralNode("<li>" + text), new ValueNode(scope => scope), new LiteralNode("</li>")]),
            new LiteralNode("</main>"),
        ];
        var eager = new RenderPlan([], body, []).Template;
        _ = new RenderPlan([], body, []);

        var start = GC.GetAllocatedBytesForCurrentThread();
        var plan = new RenderPlan([], body, []);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        var unread = new RenderPlan([], body, []);
        var template = plan.Template;

        Assert.True(allocated < text.Length * 3 / 10, $"{allocated} B allocated to create a plan of {text.Length * 3} B of literals");
        Assert.Equal(eager, template);
        Assert.Same(template, plan.Template);
        Assert.Equal(plan, unread);
        Assert.Equal(plan.GetHashCode(), unread.GetHashCode());
        Assert.NotEqual(plan, new RenderPlan([], [.. body], []));
    }

    [Fact]
    [Trait("AC", "RENDERING-26")]
    public void DynamicsGrowNoArrayWithThePage()
    {
        var rows = Enumerable.Range(0, 3000).Select(index => "row " + index.ToString(CultureInfo.InvariantCulture)).ToArray();
        var large = new string('y', 100_000);
        var plan = new RenderPlan([], [
            new LiteralNode("<ul>"), new RepeatNode(_ => rows, [new LiteralNode("<li>"), new ValueNode(row => row), new LiteralNode("</li>")]),
            new LiteralNode("</ul><p>"), new ValueNode(_ => large), new LiteralNode("</p>"),
        ], []);
        using var first = new PooledBuffer();
        plan.WriteDynamics(first, [new object()]);

        var least = long.MaxValue;
        var length = 0;
        for (var run = 0; run < 4; run++)
        {
            using var again = new PooledBuffer();
            var start = GC.GetAllocatedBytesForCurrentThread();
            plan.WriteDynamics(again, [new object()]);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - start);
            length = again.WrittenSpan.Length;
            Assert.True(first.WrittenSpan.SequenceEqual(again.WrittenSpan));
        }

        Assert.True(length > 85_000);
        Assert.StartsWith("[[[\"row 0\"],", System.Text.Encoding.UTF8.GetString(first.WrittenSpan), StringComparison.Ordinal);
        Assert.True(least < length / 4, $"{least} B allocated for dynamics of {length} B");
    }

    [Fact]
    [Trait("AC", "RENDERING-27")]
    public void ALiteralAloneBetweenSlotsIsKeptAsItIs()
    {
        var text = new string('z', 120_000);
        _ = Build(new string('w', 120_000));

        var start = GC.GetAllocatedBytesForCurrentThread();
        var nodes = Build(text);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        new RenderPlan([], nodes, []).RenderBody(output, [null]);

        Assert.True(allocated < text.Length / 10, $"{allocated} B allocated to build a plan of a {text.Length}-character literal");
        Assert.Same(text, ((LiteralNode)nodes[2]).Text);
        Assert.Equal("<b></b>", ((LiteralNode)nodes[4]).Text);
        Assert.Equal(6, nodes.Length);
        Assert.Equal("<i>" + text + "<b></b>", System.Text.Encoding.UTF8.GetString(output.WrittenSpan));
        Assert.Equal(["<p>", text, "</p>"], Apart(text).Select(node => ((LiteralNode)node).Text));
        Assert.Same(text, ((LiteralNode)Apart(text)[1]).Text);
    }

    private static ImmutableArray<IPlanNode> Apart(string text)
    {
        var builder = new PlanBuilder();
        builder.Literal("<p>");
        builder.LiteralApart(text);
        builder.Literal("</p>");
        return builder.Build();
    }

    private static ImmutableArray<IPlanNode> Build(string text)
    {
        var builder = new PlanBuilder();
        builder.Literal("<i>");
        builder.Add(new ValueNode(_ => null));
        builder.Literal(text);
        builder.Add(new ValueNode(_ => null));
        builder.Literal("<b>");
        builder.Literal(string.Empty);
        builder.Literal("</b>".AsSpan());
        builder.Add(new ValueNode(_ => null));
        return builder.Build();
    }
}
