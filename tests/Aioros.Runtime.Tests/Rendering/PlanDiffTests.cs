using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class PlanDiffTests
{
    [Fact]
    [Trait("AC", "RENDERING-13")]
    public void TemplatesAndDynamicsJoinIntoTheRenderedHtml()
    {
        ImmutableArray<IPlanNode> body =
        [
            new LiteralNode("<h1>"), new ValueNode(scope => ((Shop)scope!).Name), new LiteralNode("</h1><input name=\"f"), IdNode.Instance, new LiteralNode("\">"),
            new ConditionNode(scope => ((Shop)scope!).Open, [new LiteralNode("<p>Open "), new ValueNode(scope => ((Shop)scope!).Name), new LiteralNode("</p>")]),
            new LiteralNode("<ul>"),
            new RepeatNode(scope => ((Shop)scope!).Items, [
                new LiteralNode("<li>"), new ValueNode(scope => scope), new LiteralNode("<button name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|\">x</button>"),
                new ConditionNode(scope => ((string)scope!).Length > 3, [new LiteralNode("<b>long</b>")]),
                new LiteralNode("</li>"),
            ]),
            new LiteralNode("</ul>"), new ScopeNode(0, [new ValueNode(scope => ((Shop)scope!).Items.Length)]),
            new SelectNode(null, 1, ["a", "b"]),
        ];
        var plan = new RenderPlan([], body, []);
        var open = new Shop("Tom & <Jerry>", true, ["tea", "coffee"]);
        var closed = new Shop("Ana", false, []);

        var template = plan.Template;
        var renamed = new RenderPlan([], [new LiteralNode("<h2>"), .. body[1..]], []).Template;

        var json = new ArrayBufferWriter<byte>();
        PlanTemplate.Write(json, plan.Body);
        foreach (var shop in new[] { open, closed })
        {
            var html = new ArrayBufferWriter<byte>();
            plan.RenderBody(html, [shop]);
            var dynamics = new ArrayBufferWriter<byte>();
            plan.WriteDynamics(dynamics, [shop]);
            Assert.Equal(Encoding.UTF8.GetString(html.WrittenSpan), Join(JsonDocument.Parse(json.WrittenMemory).RootElement, JsonDocument.Parse(dynamics.WrittenMemory).RootElement));
        }

        Assert.Equal(template, new RenderPlan([], body, []).Template);
        Assert.Equal(json.WrittenCount, template.Length);
        Assert.NotEqual(template.Fingerprint, renamed.Fingerprint);
    }

    private static string Join(JsonElement template, JsonElement dynamics)
    {
        var statics = template.GetProperty("s").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var html = new StringBuilder(statics[0]);
        var index = 0;
        foreach (var value in dynamics.EnumerateArray())
        {
            var key = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (template.TryGetProperty("c", out var conditions) && conditions.TryGetProperty(key, out var condition))
            {
                html.Append(value.ValueKind == JsonValueKind.Array ? Join(condition, value) : string.Empty);
            }
            else if (template.TryGetProperty("r", out var repeats) && repeats.TryGetProperty(key, out var repeat))
            {
                foreach (var row in value.EnumerateArray())
                {
                    html.Append(Join(repeat, row));
                }
            }
            else
            {
                html.Append(value.GetString());
            }

            html.Append(statics[++index]);
        }

        return html.ToString();
    }

    private sealed record Shop(string Name, bool Open, string[] Items);
}
