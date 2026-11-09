using System.Buffers;
using System.Globalization;
using System.Text;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class RenderPlanTests
{
    private static readonly ValueGetter Name = scope => ((Person)scope!).FullName;
    private static readonly int[] TwoItems = [1, 2];

    [Fact]
    [Trait("AC", "RENDERING-01")]
    public void LiteralsAreWrittenAsUtf8()
    {
        var plan = Plan(new LiteralNode("<p>Añadir · 日本</p>"));

        var bytes = RenderBytes(plan, model: null);

        Assert.Equal(Encoding.UTF8.GetBytes("<p>Añadir · 日本</p>"), bytes);
    }

    [Fact]
    [Trait("AC", "RENDERING-02")]
    public void ValuesAreHtmlEncoded()
    {
        var plan = Plan(new ValueNode(Name));

        Assert.Equal("Tom &amp; &lt;Jerry&gt; &quot;Ñ&quot; 'x'", Render(plan, new Person("Tom & <Jerry> \"Ñ\" 'x'")));
    }

    [Fact]
    [Trait("AC", "RENDERING-03")]
    public void ValuesAreFormattedWithTheCurrentCulture()
    {
        var plan = Plan(new ValueNode(scope => scope), new LiteralNode("|"));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");

            Assert.Equal("|", Render(plan, model: null));
            Assert.Equal("1234,5|", Render(plan, 1234.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    [Trait("AC", "RENDERING-04")]
    public void RawValuesAreNotEncoded()
    {
        var plan = Plan(new ValueNode(Name, Raw: true));

        Assert.Equal("<path d=\"M0 0\"/>", Render(plan, new Person("<path d=\"M0 0\"/>")));
    }

    [Fact]
    [Trait("AC", "RENDERING-05")]
    public void ConditionsRenderTheirBodyOnlyWhenTrue()
    {
        var plan = Plan(new ConditionNode(scope => scope, [new LiteralNode("shown")]));

        Assert.Equal("shown", Render(plan, true));
        Assert.Equal(string.Empty, Render(plan, false));
        Assert.Equal(string.Empty, Render(plan, "true"));
    }

    [Fact]
    [Trait("AC", "RENDERING-06")]
    public void RepeatsRenderTheirBodyPerItem()
    {
        var plan = Plan(new RepeatNode(scope => scope, [new LiteralNode("<li>"), new ValueNode(Name), new LiteralNode("</li>")]));

        Assert.Equal("<li>Ana</li><li>Bo</li>", Render(plan, new[] { new Person("Ana"), new Person("Bo") }));
    }

    [Fact]
    [Trait("AC", "RENDERING-07")]
    public void IdsAreNumberedInDocumentOrder()
    {
        var plan = Plan(
            IdNode.Instance,
            new RepeatNode(scope => scope, [new LiteralNode(","), IdNode.Instance]),
            new LiteralNode(","),
            IdNode.Instance);

        Assert.Equal("0,1,2,3", Render(plan, TwoItems));
    }

    [Fact]
    [Trait("AC", "RENDERING-08")]
    public void SelectsMarkTheSelectedOption()
    {
        var plan = Plan(new SelectNode(Name, Selected: -1, ["Low", "High & urgent"]));

        Assert.Equal(
            "<option value=\"0\">Low</option><option value=\"1\" selected>High &amp; urgent</option>",
            Render(plan, new Person("High & urgent")));
    }

    [Fact]
    [Trait("AC", "RENDERING-09")]
    public void BodiesRenderWithoutHeadAndTail()
    {
        var plan = new RenderPlan([new LiteralNode("<head>")], [new LiteralNode("<main>")], [new LiteralNode("</html>")]);
        var writer = new ArrayBufferWriter<byte>();

        plan.RenderBody(writer, [null]);

        Assert.Equal("<main>", Encoding.UTF8.GetString(writer.WrittenSpan));
    }

    [Fact]
    [Trait("AC", "RENDERING-10")]
    public void BuildersMergeConsecutiveLiterals()
    {
        var builder = new PlanBuilder();
        builder.Literal("<ul>");
        builder.Literal("<li>");
        builder.Begin();
        builder.Literal("a");
        builder.Literal("b");
        builder.Add(new RepeatNode(scope => scope, builder.End()));
        builder.Literal("</li>");
        builder.Literal("</ul>");

        var nodes = builder.Build();

        Assert.Collection(
            nodes,
            node => Assert.Equal("<ul><li>", Assert.IsType<LiteralNode>(node).Text),
            node => Assert.Equal("ab", Assert.IsType<LiteralNode>(Assert.Single(Assert.IsType<RepeatNode>(node).Body)).Text),
            node => Assert.Equal("</li></ul>", Assert.IsType<LiteralNode>(node).Text));
    }

    [Fact]
    [Trait("AC", "RENDERING-11")]
    public void ScopesReadFromAnotherModel()
    {
        var plan = Plan(new ValueNode(Name), new LiteralNode("/"), new ScopeNode(1, [new ValueNode(Name)]));
        var writer = new ArrayBufferWriter<byte>();

        plan.Render(writer, [new Person("Layout"), new Person("Page")]);

        Assert.Equal("Layout/Page", Encoding.UTF8.GetString(writer.WrittenSpan));
    }

    [Fact]
    [Trait("AC", "RENDERING-12")]
    public void PlansKnowWhetherTheyHaveFields()
    {
        Assert.False(Plan(new LiteralNode("<p>"), new ValueNode(Name)).HasFields);
        Assert.True(Plan(IdNode.Instance).HasFields);
        Assert.True(Plan(new ConditionNode(scope => scope, [new RepeatNode(scope => scope, [new ScopeNode(0, [IdNode.Instance])])])).HasFields);
        Assert.True(Plan(new TypeSwitchNode([new TypeCase(typeof(string), []), new TypeCase(typeof(object), [IdNode.Instance])])).HasFields);
        Assert.False(Plan(new TypeSwitchNode([new TypeCase(typeof(object), [new LiteralNode("<p>")])])).HasFields);
    }

    private static RenderPlan Plan(params IPlanNode[] body) => new([], [.. body], []);

    private static byte[] RenderBytes(RenderPlan plan, object? model)
    {
        var writer = new ArrayBufferWriter<byte>();
        plan.Render(writer, [model]);
        return writer.WrittenSpan.ToArray();
    }

    private static string Render(RenderPlan plan, object? model) => Encoding.UTF8.GetString(RenderBytes(plan, model));

    private sealed record Person(string FullName);
}
