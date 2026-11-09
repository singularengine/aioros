using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PrecompileReportTests
{
    [Fact]
    [Trait("AC", "PRODUCTION-11")]
    public void PagesThatDoNotCompileFailThePrecompilation()
    {
        var planned = new WarmupOutcome("/", [], null);
        var redirect = new WarmupOutcome("/old", [], "its model redirects to /inbox") { Skipped = true };
        var parameters = new WarmupOutcome("/games/{id}", [], "route parameters without a sample URL");
        var dynamic = new WarmupOutcome("/lab", [], "not reusable: Button.Content is bound") { At = new SourcePosition("/app/Pages/lab/Page.axaml", 12, 6) };
        var stored = new WarmupOutcome("/stored", [], null);

        var clean = PrecompileReport.Of([planned, redirect], []);
        var failed = PrecompileReport.Of([planned, redirect, parameters, dynamic, stored], [new PlansFileSkip("/stored", "light", "a value slot without a description")]);

        Assert.False(clean.Failed);
        Assert.Contains("precompiled  /", clean.Text, StringComparison.Ordinal);
        Assert.Contains("skipped      /old  (its model redirects to /inbox)", clean.Text, StringComparison.Ordinal);
        Assert.True(failed.Failed);
        Assert.Equal(3, failed.Errors.Length);
        Assert.Contains("error AIOROS010: /games/{id}: route parameters without a sample URL", failed.Text, StringComparison.Ordinal);
        Assert.Contains("IPrecompileSource", failed.Text, StringComparison.Ordinal);
        Assert.Contains("\n  /app/Pages/lab/Page.axaml(12,6): error AIOROS010: /lab: not reusable: Button.Content is bound\n", failed.Text, StringComparison.Ordinal);
        Assert.Contains("/app/Pages/lab/Page.axaml(12,6): error AIOROS010: /lab: not reusable: Button.Content is bound", failed.Errors);
        Assert.Contains("error AIOROS010: /stored: not serializable: a value slot without a description", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-35")]
    public void FieldsLeftToTheEngineAreReportedKeptAndPreloaded()
    {
        var text = new PostedField("f0", "text", "Report");
        var outcome = new WarmupOutcome("/parity", [], null) { Diverging = [new FieldVerdict(text, "Reportx", false, "plan: …"), new FieldVerdict(text, "", false, "plan: …")], Unverified = 12 };
        var shared = new WarmupOutcome("/shared", [], null) { SharedModels = true };
        var page = new PlannedPage("/parity", "light", new CompiledPage(new RenderPlan([], [new LiteralNode("<p>Report</p>")], []), ".page{}\n", "#fff", "#000", true)) { EngineFields = ["f0", "_btn:3|"] };
        var cache = new PlanCache();

        var report = PrecompileReport.Of([outcome, shared], []);
        var read = PlansFile.Read(PlansFile.Write([page]).Json, new Aioros.Runtime.Slots.PlanSlotResolver()).Single();
        cache.Preload([read], [new Aioros.Pages.PageDefinition("/parity", "Pages/parity/Page.axaml", static _ => null, [])], new Aioros.Targets.Ssr.Hosting.SsrOptions());

        Assert.False(report.Failed);
        Assert.Contains("precompiled  /parity  (the engine answers posts of f0 (text): the plan answers them otherwise; first difference: plan: …)  (12 fields not verified: past the verification's budget)", report.Text, StringComparison.Ordinal);
        Assert.Contains("precompiled  /shared  (posts not verified: its models are shared by the app)", report.Text, StringComparison.Ordinal);
        Assert.Equal(["f0", "_btn:3|"], read.EngineFields);
        Assert.Equal(["_btn:3|", "f0"], cache.EngineFields("/parity").Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("AC", "SSR-308")]
    public void FieldsADisabledFieldsetHoldsAreNotPosted()
    {
        const string Html = """
            <input type="text" name="f0" value="kept"><dialog><fieldset disabled><input type="text" name="f1" value="closed"><fieldset><select name="f2"><option value="a" selected>A</option></select></fieldset><input name="f3" value="x"></fieldset></dialog><fieldset><input type="checkbox" name="f4" value="on" checked></fieldset>
            """;

        var posted = PostedForms.Of(Html);

        Assert.Equal(["f0", "f4"], posted.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("AC", "PRODUCTION-31")]
    public void PagesWhosePlanAFreshModelMissesFailThePrecompilation()
    {
        var plan = new RenderPlan([], [new LearnedNode(static model => ((Chosen)model!).Choice, [LearnedNode.KeyOf("first")], [[new LiteralNode("<p>first</p>")]]) { Source = SlotSpec.Bound("Choice") }], []);
        var missed = PlanComparison.Miss(plan, [new Chosen(null)], "/command");
        var rendered = PlanComparison.Miss(plan, [new Chosen("first")], "/command");
        var outcomes = new[] { new WarmupOutcome("/", [], null), new WarmupOutcome("/command", [], null) };

        var report = PrecompileReport.Of(outcomes, []).Checked([new PlanCheck("/", "light", null), new PlanCheck("/command", "dark", null) { Miss = missed }], new AccessorCoverage(0, 0, []));

        Assert.Null(rendered);
        Assert.Contains("the value '' of 'Choice'", missed, StringComparison.Ordinal);
        Assert.True(report.Failed);
        Assert.StartsWith("Precompiled 1 of 2 pages\n", report.Text, StringComparison.Ordinal);
        Assert.Equal([$"error AIOROS010: /command (dark): a fresh model misses its plan: {missed}"], report.Errors);
        Assert.Contains("Fresh models: 1 of 2 pages and themes render from their saved plan for models loaded anew", report.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-28")]
    public void EveryPageLeftOutStatesItsReason()
    {
        var wrapped = new System.Reflection.TargetInvocationException(new InvalidOperationException("boom"));
        var silent = new InvalidOperationException(string.Empty);
        var outcomes = new[]
        {
            new WarmupOutcome("/", [], null),
            new WarmupOutcome("/issues", [], null),
            new WarmupOutcome("/wrapped", [], wrapped.Message, wrapped),
            new WarmupOutcome("/silent", [], silent.Message, silent),
            new WarmupOutcome("/blank", [], " "),
        };

        var report = PrecompileReport.Of(outcomes, []).Checked([new PlanCheck("/", "light", null), new PlanCheck("/issues", "dark", 3)], new AccessorCoverage(0, 0, []));

        Assert.StartsWith("Precompiled 1 of 5 pages\n", report.Text, StringComparison.Ordinal);
        Assert.Equal(
            [
                "error AIOROS010: /wrapped: InvalidOperationException: boom",
                "error AIOROS010: /silent: InvalidOperationException",
                "error AIOROS010: /blank: no reason was recorded",
                "error AIOROS010: /issues (dark): generated accessors and runtime delegates render different bytes from offset 3",
            ],
            report.Errors);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-14")]
    public void PagesMustRenderTheSameBytesWithGeneratedAccessorsAndRuntimeDelegates()
    {
        static RenderPlan Plan(string value) => new([], [new LiteralNode("<p>"), new ValueNode(_ => value), new LiteralNode("</p>")], []);
        var outcomes = new[] { new WarmupOutcome("/", [], null), new WarmupOutcome("/issues", [], null) };
        var coverage = new AccessorCoverage(8, 10, ["App.Plain.Name", "App.Plain.Name (set)"]) { GeneratedWrites = 2, Writes = 3 };

        var same = PlanComparison.FirstDifference(Plan("abc"), Plan("abc"), [new object()]);
        var different = PlanComparison.FirstDifference(Plan("abc"), Plan("abd"), [new object()]);
        var longer = PlanComparison.FirstDifference(Plan("abc"), Plan("abcd"), [new object()]);
        var clean = PrecompileReport.Of(outcomes, []).Checked([new PlanCheck("/", "light", null), new PlanCheck("/issues", "dark", null)], new AccessorCoverage(10, 10, []) { GeneratedWrites = 4, Writes = 4 });
        var failed = PrecompileReport.Of(outcomes, []).Checked([new PlanCheck("/", "light", null), new PlanCheck("/issues", "dark", different)], coverage);

        Assert.Equal((null, 5, 6), (same, different, longer));
        Assert.False(clean.Failed);
        Assert.Contains("Identical output: 2 of 2 pages and themes render the same bytes with generated accessors and with runtime delegates", clean.Text, StringComparison.Ordinal);
        Assert.Contains("Generated accessors served 10 of 10 member reads and writes (4 of 4 writes)\n", clean.Text, StringComparison.Ordinal);
        Assert.True(failed.Failed);
        Assert.Equal(["error AIOROS010: /issues (dark): generated accessors and runtime delegates render different bytes from offset 5"], failed.Errors);
        Assert.Contains("Identical output: 1 of 2 pages", failed.Text, StringComparison.Ordinal);
        Assert.Contains("Generated accessors served 8 of 10 member reads and writes (2 of 3 writes); read at run time: App.Plain.Name, App.Plain.Name (set)", failed.Text, StringComparison.Ordinal);
    }

    private sealed record Chosen(string? Choice);
}
