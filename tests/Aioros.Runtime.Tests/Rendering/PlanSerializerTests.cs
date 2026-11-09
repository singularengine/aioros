using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class PlanSerializerTests
{
    [Fact]
    [Trait("AC", "PRODUCTION-01")]
    public void DescribedPlansRoundTrip()
    {
        var resolver = new Resolver();
        ImmutableArray<IPlanNode> body =
        [
            new LiteralNode("<h1>"),
            Value("Name", new SlotSpec("upper")),
            new LiteralNode("</h1>"),
            new ConditionNode(resolver.Getter(SlotSpec.Bound("Show")), [new LiteralNode("<p>shown</p>")]) { Source = SlotSpec.Bound("Show") },
            new RepeatNode(resolver.Getter(SlotSpec.Bound("Tags")), [new LiteralNode("<i>"), Value("self", null), new LiteralNode("</i>"), IdNode.Instance]) { Source = SlotSpec.Bound("Tags") },
            new SelectNode(resolver.Getter(SlotSpec.Bound("Name")), -1, ["Ana", "Bo"]) { Source = SlotSpec.Bound("Name") },
            new ScopeNode(1, [Value("Name", null)]),
            new ChoiceNode(
                resolver.Getter(SlotSpec.Bound("Tags")),
                resolver.Predicate(new SlotSpec("index-path", "Selected")),
                new ChoiceMarkup("<label>", "<label class=\"on\">", "<input type=\"radio\"", "</label>"),
                [[Value("self", null)]])
            { ItemsSource = SlotSpec.Bound("Tags"), Selection = new SlotSpec("index-path", "Selected") },
        ];
        var write = PlanSerializer.TryWrite(body);

        var read = PlanSerializer.Read(write.Json!, resolver);

        Assert.Null(write.Failure);
        foreach (var model in new[] { new Model("Ana", true, ["x", "y"], 1), new Model("Bo", false, [], 0) })
        {
            Assert.Equal(Render(body, model), Render(read, model));
        }
    }

    [Fact]
    [Trait("AC", "PRODUCTION-02")]
    public void UndescribedSlotsAreReported()
    {
        var write = PlanSerializer.TryWrite([new LiteralNode("<p>"), new ValueNode(scope => scope)]);
        var nested = PlanSerializer.TryWrite([new ScopeNode(0, [new RepeatNode(scope => scope, [new ValueNode(scope => scope)]) { Source = SlotSpec.Bound("Tags") }])]);

        Assert.Null(write.Json);
        Assert.Equal("a value slot without a description", write.Failure);
        Assert.Null(nested.Json);
        Assert.Equal("a value slot without a description", nested.Failure);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-04")]
    public void PlansFilesKeepPagesAndReportTheRest()
    {
        var plan = new RenderPlan([new LiteralNode("<b>")], [Value("Name", new SlotSpec("upper"))], [new LiteralNode("</b>")]);
        PlannedPage[] pages =
        [
            new("/", "light", new CompiledPage(plan, ".a{}", "#fff", "#111", true)),
            new("/", "dark", new CompiledPage(plan, ".b{}", "#000", "#eee", true)),
            new("/raw", "light", new CompiledPage(new RenderPlan([], [new ValueNode(scope => scope)], []), string.Empty, "#fff", "#111", true)),
        ];

        var write = PlansFile.Write(pages);
        var read = PlansFile.Read(write.Json, new Resolver());

        var skipped = Assert.Single(write.Skipped);
        Assert.Equal(new PlansFileSkip("/raw", "light", "a value slot without a description"), skipped);
        Assert.Equal(2, read.Length);
        Assert.Equal(("/", "dark", ".b{}", "#000", "#eee", true), (read[1].Route, read[1].Theme, read[1].Page.Css, read[1].Page.Background, read[1].Page.Foreground, read[1].Page.Reusable));
        var model = new Model("Ana", true, [], 0);
        Assert.Equal(RenderPage(plan, model), RenderPage(read[0].Page.Plan, model));
    }

    [Fact]
    [Trait("AC", "PRODUCTION-07")]
    public async Task FormsAreAppliedThroughDescribedFieldsAsync()
    {
        var resolver = new Forms();
        var command = new SlotSpec("command", "Name");
        var plan = new RenderPlan([], [new IdNode { Field = new SlotSpec("text", "Name") }, new RepeatNode(resolver.Getter(SlotSpec.Bound("Tags")), [new IdNode { Field = command, Button = true }])], []);
        var undescribed = new RenderPlan([], [IdNode.Instance], []);
        var model = new Model("Ana", true, ["x", "y"], 0);

        var declined = await PlanForms.ApplyAsync(undescribed, [model], ImmutableDictionary<string, string>.Empty, resolver);
        var malformed = await PlanForms.ApplyAsync(plan, [model], Form(("_btn", "two")), resolver);
        var mismatched = await PlanForms.ApplyAsync(plan, [model], Form(("_btn", "1|x")), resolver);
        var applied = await PlanForms.ApplyAsync(plan, [model], Form(("f0", "Bo"), ("_btn", "1|y")), resolver);

        Assert.Equal((null, PostStatus.BadRequest, PostStatus.Conflict, PostStatus.Rendered), (declined, malformed, mismatched, applied));
        Assert.Equal(["text:Bo", "run:y:y"], resolver.Log[^2..]);
    }

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value);

    [Fact]
    [Trait("AC", "PRODUCTION-29")]
    public void PlansThatNeverReadAnOuterScopeKeepNone()
    {
        static RenderPlan Plan(string path)
        {
            var resolver = new Resolver();
            var tags = SlotSpec.Bound("Tags");
            var outer = SlotSpec.Bound(path);
            return new RenderPlan([], [new RepeatNode(resolver.Getter(tags), [new ValueNode(resolver.Getter(outer)) { Source = outer }, new LiteralNode("|")]) { Source = tags }], []);
        }

        var read = PlansFile.Read(PlansFile.Write(
        [
            new PlannedPage("/parent", "light", new CompiledPage(Plan("^1.Name"), string.Empty, "#fff", "#111", true)),
            new PlannedPage("/none", "light", new CompiledPage(Plan("Host"), string.Empty, "#fff", "#111", true)),
        ]).Json, new Resolver()).ToDictionary(page => page.Route, page => page.Page.Plan);
        var model = new Model("Board", Show: true, ["a", "b"], Selected: 0);

        Assert.Equal((true, false), (read["/parent"].ReadsOuter, read["/none"].ReadsOuter));
        Assert.Equal("Board|Board|", RenderPage(read["/parent"], model));
        Assert.Equal("none|none|", RenderPage(read["/none"], model));
        Assert.Equal("Board|Board|", RenderPage(Plan("Host"), model));
    }

    private static string RenderPage(RenderPlan plan, Model model)
    {
        var writer = new ArrayBufferWriter<byte>();
        plan.Render(writer, [model]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private static ValueNode Value(string path, SlotSpec? format)
    {
        var resolver = new Resolver();
        var source = path == "self" ? new SlotSpec("self") : SlotSpec.Bound(path);
        return new ValueNode(resolver.Getter(source), resolver.Formatter(format)) { Source = source, FormatSpec = format };
    }

    private static string Render(ImmutableArray<IPlanNode> body, Model model)
    {
        var writer = new ArrayBufferWriter<byte>();
        new RenderPlan([], body, []).Render(writer, [model, model with { Name = "Scoped" }]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private sealed record Model(string Name, bool Show, ImmutableArray<string> Tags, int Selected);

    private sealed class Forms : IFieldResolver, ISlotResolver
    {
        private readonly Resolver _slots = new();

        public List<string> Log { get; } = [];

        public FieldBinding? Binding(SlotSpec field) => field.Kind == "text" ? (_, value) => Log.Add("text:" + value) : null;

        public FieldCommand? Command(SlotSpec field) => field.Kind == "command"
            ? new FieldCommand(scope => scope, (scope, parameter) =>
            {
                Log.Add($"run:{scope}:{parameter}");
                return ValueTask.CompletedTask;
            })
            : null;

        public ValueGetter Getter(SlotSpec spec) => _slots.Getter(spec);

        public ValueFormatter? Formatter(SlotSpec? spec) => _slots.Formatter(spec);

        public ChoicePredicate Predicate(SlotSpec spec) => _slots.Predicate(spec);
    }

    private sealed class Resolver : ISlotResolver
    {
        public ValueGetter Getter(SlotSpec spec) => (spec.Kind, spec.Path) switch
        {
            ("self", _) => scope => scope,
            (_, "Name") => scope => ((Model)scope!).Name,
            (_, "Show") => scope => ((Model)scope!).Show,
            (_, "Tags") => scope => ((Model)scope!).Tags,
            (_, "Selected") => scope => ((Model)scope!).Selected,
            (_, "^1.Name" or "Host") => _ => OuterScopes.At(1) is Model outer ? outer.Name : "none",
            _ => throw new ArgumentException(spec.Path, nameof(spec)),
        };

        public ValueFormatter? Formatter(SlotSpec? spec) => spec?.Kind == "upper" ? value => PlanValues.Format(value).ToUpperInvariant() : null;

        public ChoicePredicate Predicate(SlotSpec spec)
        {
            var selected = Getter(SlotSpec.Bound(spec.Path!));
            return (scope, index, _) => selected(scope) is int value && value == index;
        }
    }
}
