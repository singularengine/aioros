using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class ViewNodeTests
{
    private const string Key = "Pages/home/Page.axaml:0.1";

    private static readonly ValueGetter Name = scope => ((Person)scope!).FullName;

    private static readonly ValueGetter Host = _ => OuterScopes.At(1) is Person person ? person.FullName : "isolated";

    [Fact]
    [Trait("AC", "RENDERING-15")]
    public void ViewsRenderTheirOwnModelIsolatedFromTheHost()
    {
        var plan = new RenderPlan([], [
            new LiteralNode("<p>"), new ValueNode(Name), new LiteralNode("</p>"),
            new ScopeNode(1, [new ViewNode(2, Key, [new ValueNode(Name), new LiteralNode("|"), new ValueNode(Host)], [new LiteralNode("<em>failed</em>")])]),
        ], []);

        var shown = Render(plan, new Person("Layout"), new Person("Page"), new Person("View"));
        var hidden = Render(plan, new Person("Layout"), new Person("Page"), ViewState.Hidden);
        var failed = Render(plan, new Person("Layout"), new Person("Page"), ViewState.Failed);

        Assert.Equal($"<p>Layout</p><div data-aioros-view=\"{Key}\" style=\"display:contents\">View|isolated</div>", shown);
        Assert.Equal("<p>Layout</p>", hidden);
        Assert.Equal($"<p>Layout</p><div data-aioros-view=\"{Key}\" data-view-error role=\"alert\" style=\"display:contents\"><em>failed</em></div>", failed);
    }

    [Fact]
    [Trait("AC", "RENDERING-16")]
    public void ViewsAreSerializedScannedAndOneDynamicSlot()
    {
        var name = SlotSpec.Bound("FullName");
        ImmutableArray<IPlanNode> body =
        [
            new LiteralNode("<main>"),
            new ViewNode(1, Key, [new ValueNode(Name) { Source = name }], [new LiteralNode("<em>failed</em>")]),
            new LiteralNode("</main>"),
        ];
        var plan = new RenderPlan([], body, []);
        var write = PlanSerializer.TryWrite(body);
        var read = new RenderPlan([], PlanSerializer.Read(write.Json!, new Resolver()), []);
        var dynamics = new ArrayBufferWriter<byte>();
        plan.WriteDynamics(dynamics, [null, new Person("View")]);
        var template = new ArrayBufferWriter<byte>();
        PlanTemplate.Write(template, plan.Body);
        var statics = JsonDocument.Parse(template.WrittenMemory).RootElement.GetProperty("s").EnumerateArray().Select(item => item.GetString()).ToList();

        Assert.Null(write.Failure);
        foreach (var view in new object?[] { new Person("View"), ViewState.Failed, ViewState.Hidden })
        {
            Assert.Equal(Render(plan, null, view), Render(read, null, view));
        }

        Assert.False(plan.HasFields);
        Assert.True(new RenderPlan([], [new ViewNode(0, Key, [IdNode.Instance], [])], []).HasFields);
        Assert.Equal(["<main>", "</main>"], statics);
        Assert.Equal([Render(new RenderPlan([], [body[1]], []), null, new Person("View"))], JsonDocument.Parse(dynamics.WrittenMemory).RootElement.EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    [Trait("AC", "RENDERING-17")]
    public void FieldIdsAreNamespacedPerViewAndARegionRendersAlone()
    {
        static IPlanNode Field() => new IdNode { Field = SlotSpec.Bound("FullName") };
        ImmutableArray<IPlanNode> Input(string tag) => [new LiteralNode("<" + tag + " name=\"f"), Field(), new LiteralNode("\">")];
        var nested = new ViewNode(3, "inner", [.. Input("i"), new LiteralNode("<r name=\"f"), new IdNode { Field = SlotSpec.Bound("FullName"), Key = "g" }, new LiteralNode("\"><r name=\"f"), new SameIdNode("g"), new LiteralNode("\">")], []);
        var choice = new ChoiceNode(null, (_, index, _) => index == 0, new ChoiceMarkup("<c>", "<c>", "<input type=\"radio\"", "</c>"), [[], []]) { Field = SlotSpec.Bound("FullName") };
        var hidden = new ConditionNode(_ => false, [new ViewNode(4, "hidden", [.. Input("h")], [])]);
        var plan = new RenderPlan([], [
            .. Input("a"),
            new ViewNode(2, Key, [.. Input("v"), choice, nested, .. Input("w")], []),
            .. Input("b"),
            hidden,
            .. Input("c"),
        ], []);
        object?[] models = [new Person("Layout"), new Person("Page"), new Person("View"), new Person("Inner"), new Person("Hidden")];

        var page = Render(plan, models);
        var fields = PlanForms.Fields(plan, [.. models]);
        var region = new ArrayBufferWriter<byte>();
        var found = plan.RenderView(region, [.. models], 2);
        var inner = new ArrayBufferWriter<byte>();
        var innerFound = plan.RenderView(inner, [.. models], 3);

        Assert.Equal(
            $"<a name=\"f0\"><div data-aioros-view=\"{Key}\" style=\"display:contents\"><v name=\"f2.0\"><c><input type=\"radio\" name=\"f2.1\" value=\"0\" checked></c><c><input type=\"radio\" name=\"f2.1\" value=\"1\"></c>"
            + "<div data-aioros-view=\"inner\" style=\"display:contents\"><i name=\"f3.0\"><r name=\"f3.1\"><r name=\"f3.1\"></div><w name=\"f2.2\"></div><b name=\"f1\"><c name=\"f2\">",
            page);
        Assert.Equal(["0", "2.0", "2.1", "3.0", "3.1", "2.2", "1", "2"], fields.Fields.Select(field => field.Token));
        Assert.Equal([-1, 2, 2, 3, 3, 2, -1, -1], fields.Fields.Select(field => field.View));
        Assert.True(found);
        Assert.True(innerFound);
        Assert.Contains(Encoding.UTF8.GetString(region.WrittenSpan), page, StringComparison.Ordinal);
        Assert.StartsWith($"<div data-aioros-view=\"{Key}\"", Encoding.UTF8.GetString(region.WrittenSpan), StringComparison.Ordinal);
        Assert.Equal("<div data-aioros-view=\"inner\" style=\"display:contents\"><i name=\"f3.0\"><r name=\"f3.1\"><r name=\"f3.1\"></div>", Encoding.UTF8.GetString(inner.WrittenSpan));
        Assert.False(plan.RenderView(new ArrayBufferWriter<byte>(), [.. models], 4));
        Assert.False(plan.RenderView(new ArrayBufferWriter<byte>(), [.. models], 1));
    }

    [Fact]
    [Trait("AC", "RENDERING-18")]
    public void RepeatedViewsRenderEachItemsModelInItsOwnRegion()
    {
        var cards = new[] { new Card("a.1"), new Card("b-2"), new Card("c") };
        var items = new ViewItems([new ViewItem("a.1", cards[0], new Person("A")), new ViewItem("b-2", cards[1], ViewState.Hidden), new ViewItem("c", cards[2], ViewState.Failed)], item => ((Card)item!).Id);
        var unkeyed = new ViewItems([new ViewItem("0", cards[0], new Person("First")), new ViewItem("1", cards[1], new Person("Second"))], null);
        ImmutableArray<IPlanNode> body =
        [
            new LiteralNode("<main>"),
            new RepeatNode(board => ((Card[])board!), [new ViewNode(1, Key, [new LiteralNode("<v name=\"f"), new IdNode { Field = SlotSpec.Bound("FullName") }, new LiteralNode("\">"), new ValueNode(Name) { Source = SlotSpec.Bound("FullName") }], [new LiteralNode("<em>failed</em>")])]) { Source = SlotSpec.Bound("Cards") },
            new LiteralNode("</main>"),
        ];
        var plan = new RenderPlan([], body, []);
        ImmutableArray<object?> models = [cards, items];
        var page = Render(plan, cards, items);
        var fields = PlanForms.Fields(plan, models);
        var region = new ArrayBufferWriter<byte>();
        var found = plan.RenderView(region, models, 1, "a.1");
        var write = PlanSerializer.TryWrite(body);
        var read = new RenderPlan([], PlanSerializer.Read(write.Json!, new Resolver()), []);

        Assert.Equal(
            $"<main><div data-aioros-view=\"{Key}[a.1]\" style=\"display:contents\"><v name=\"f1-a%2E1.0\">A</div>"
            + $"<div data-aioros-view=\"{Key}[c]\" data-view-error role=\"alert\" style=\"display:contents\"><em>failed</em></div></main>",
            page);
        Assert.Equal(["1-a%2E1.0"], fields.Fields.Select(field => field.Token));
        Assert.Equal([(1, "a.1")], fields.Fields.Select(field => (field.View, field.Item)));
        Assert.Equal(new ViewTarget(1, "b-2"), PlanForms.ViewOf(ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, "1-b%2D2.0|x")));
        Assert.True(found);
        Assert.Equal($"<div data-aioros-view=\"{Key}[a.1]\" style=\"display:contents\"><v name=\"f1-a%2E1.0\">A</div>", Encoding.UTF8.GetString(region.WrittenSpan));
        Assert.False(plan.RenderView(new ArrayBufferWriter<byte>(), models, 1, "gone"));
        Assert.False(plan.RenderView(new ArrayBufferWriter<byte>(), models, 1, "b-2"));
        Assert.Null(write.Failure);
        Assert.Equal(page, Render(read, cards, items));
        Assert.Contains($"data-aioros-view=\"{Key}[1]\" style=\"display:contents\"><v name=\"f1-1.0\">Second</div>", Render(plan, cards, unkeyed), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RENDERING-19")]
    public void AChangedFieldPicksTheViewAPostBinds()
    {
        static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) => fields.ToImmutableDictionary(field => field.Key, field => field.Value);

        Assert.Equal(new ViewTarget(2), PlanForms.ViewOf(Form((PlanForms.ChangeField, "2.0"))));
        Assert.Equal(new ViewTarget(1, "b-2"), PlanForms.ViewOf(Form((PlanForms.ChangeField, "1-b%2D2.3"))));
        Assert.Equal(ViewTarget.Page, PlanForms.ViewOf(Form((PlanForms.ChangeField, "4"))));
        Assert.Equal(ViewTarget.Page, PlanForms.ViewOf(Form(("f0", "1"))));
        Assert.Equal(new ViewTarget(3), PlanForms.ViewOf(Form((PlanForms.ChangeField, "2.0"), (PlanForms.ButtonField, "3.1|x"))));
        Assert.Null(PlanForms.ViewOf(Form((PlanForms.ChangeField, "two.zero"))));
    }

    private static string Render(RenderPlan plan, params object?[] models)
    {
        var writer = new ArrayBufferWriter<byte>();
        plan.RenderBody(writer, [.. models]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private sealed record Person(string FullName);

    private sealed record Card(string Id);

    private sealed class Resolver : ISlotResolver
    {
        public ValueGetter Getter(SlotSpec spec) => spec.Path switch
        {
            "FullName" => Name,
            "Cards" => board => board,
            _ => throw new ArgumentException(spec.Path, nameof(spec)),
        };

        public ValueFormatter? Formatter(SlotSpec? spec) => null;

        public ChoicePredicate Predicate(SlotSpec spec) => throw new ArgumentException(spec.Path, nameof(spec));
    }
}
