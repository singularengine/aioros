using System.Collections.Immutable;
using System.Globalization;
using Aioros.Runtime.Rendering;
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class LargePages
{
    private readonly string _markup = new('m', 120_000);
    private ImmutableArray<IPlanNode> _body;
    private RenderPlan _plan = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rows = Enumerable.Range(0, 3000).Select(index => "row " + index.ToString(CultureInfo.InvariantCulture)).ToArray();
        _body =
        [
            new LiteralNode("<main>" + _markup), new ValueNode(scope => scope), new LiteralNode(_markup),
            new RepeatNode(_ => rows, [new LiteralNode("<li>"), new ValueNode(row => row), new LiteralNode("</li>")]),
            new LiteralNode("</main><p>"), new ValueNode(_ => _markup), new LiteralNode("</p>"),
        ];
        _plan = new RenderPlan([], _body, []);
    }

    [Benchmark(Description = "dynamics of 3,000 rows and a 120 KB value")]
    public int Dynamics()
    {
        using var buffer = new PooledBuffer();
        _plan.WriteDynamics(buffer, ["model"]);
        return buffer.WrittenSpan.Length;
    }

    [Benchmark(Description = "plan created over 240 KB of literals")]
    public bool Created() => new RenderPlan([], _body, []).HasFields;

    [Benchmark(Description = "120 KB markup between slots, built")]
    public int Built()
    {
        var builder = new PlanBuilder();
        builder.Literal("<div class=\"chart\">");
        builder.Literal(_markup);
        builder.Add(IdNode.Instance);
        builder.Literal("</div>");
        return builder.Build().Length;
    }
}
