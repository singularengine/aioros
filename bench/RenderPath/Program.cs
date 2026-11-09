using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using Aioros.Runtime.Rendering;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(IssueRows).Assembly).Run(args);

public sealed record Issue(int Id, string Key, string Title, string Status, DateTime Updated);

[MemoryDiagnoser]
public class IssueRows
{
    private readonly ArrayBufferWriter<byte> _buffer = new(1 << 16);
    private ImmutableArray<IPlanNode> _row;
    private RepeatNode _repeat = null!;
    private Issue[] _issues = [];

    [Params(10, 50, 200)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _issues = [.. Enumerable.Range(1, Rows).Select(i => new Issue(i, "LUM-" + i, "Fix the <sidebar> & its focus ring " + i, i % 3 == 0 ? "In progress" : "Todo", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-i)))];
        _row =
        [
            new LiteralNode("<a class=\"row\" href=\"/issues/"),
            new ValueNode(o => ((Issue)o!).Id),
            new LiteralNode("\"><span class=\"key\">"),
            new ValueNode(o => ((Issue)o!).Key),
            new LiteralNode("</span><span class=\"title\">"),
            new ValueNode(o => ((Issue)o!).Title),
            new LiteralNode("</span><span class=\"status\">"),
            new ValueNode(o => ((Issue)o!).Status),
            new LiteralNode("</span><time>"),
            new ValueNode(o => ((Issue)o!).Updated, v => ((DateTime)v!).ToString("d MMM", CultureInfo.InvariantCulture)),
            new LiteralNode("</time></a>"),
        ];
        var issues = _issues;
        _repeat = new RepeatNode(_ => issues, _row);
    }

    [Benchmark(Baseline = true, Description = "rows, no publication")]
    public int WithoutPublication()
    {
        _buffer.ResetWrittenCount();
        var output = new HtmlOutput(_buffer);
        var scope = new RenderScope(this, []);
        var around = scope.Around();
        foreach (var issue in _issues)
        {
            var item = around.Item(scope, issue);
            foreach (var node in _row.AsSpan())
            {
                node.Write(ref output, item);
            }
        }

        return _buffer.WrittenCount;
    }

    [Benchmark(Description = "rows, outer scopes published per body")]
    public int WithPublication()
    {
        _buffer.ResetWrittenCount();
        var output = new HtmlOutput(_buffer);
        _repeat.Write(ref output, new RenderScope(this, []));
        return _buffer.WrittenCount;
    }
}
