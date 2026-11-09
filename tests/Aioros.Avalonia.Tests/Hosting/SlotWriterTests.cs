using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed partial class SlotWriterTests(EngineFixture fixture)
{
    private const string Lined = """<m:Plot Points="{Binding Dots}" HorizontalAlignment="Left"><m:Plot.Lines><m:PlotLine Stroke="{Binding Color}" /><m:PlotLine Stroke="green" /></m:Plot.Lines></m:Plot>""";

    private const string Windowed = """<m:Plot Points="{Binding Dots}"><m:Plot.Window><m:PlotWindow From="{Binding From}" /></m:Plot.Window></m:Plot>""";

    private const string Moded = """<m:Plot Points="{Binding Dots}" Modes="True"><m:Plot.Window><m:PlotWindow From="{Binding From}" Mode="{Binding Mode}" /></m:Plot.Window></m:Plot>""";

    private const string Slid = """<m:Plot Points="{Binding Dots}" Slide="from,to"><m:Plot.Window><m:PlotWindow From="{Binding From}" To="{Binding To}" /></m:Plot.Window></m:Plot>""";

    private const string Leveled = """<m:Plot Points="{Binding Dots}" Leveled="True"><m:Plot.Window><m:PlotWindow From="{Binding From}" Level="{Binding Level}" /></m:Plot.Window></m:Plot>""";

    private const string Picked = """<m:Plot Points="{Binding Dots}" PickCommand="{Binding Pick}"><m:Plot.Window><m:PlotWindow From="{Binding From}" /></m:Plot.Window></m:Plot>""";

    [Fact]
    [Trait("AC", "ENGINE-182")]
    public async Task ABoundValueIsWrittenByTheLibraryWriterPerRenderAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            """<m:Bars Points="{Binding Bars}" AutomationProperties.Name="Sales" HorizontalAlignment="Left" />""",
            new Barred([new Bar(1), new Bar(2)]), new Barred([new Bar(5)]), new Barred([]));

        Assert.Contains(planned.Plan.Body, node => node is WriterNode);
        Assert.Matches("<div role=\"img\" aria-label=\"Sales\" class=\"[^\"]+\"><svg data-a=\"fill=#123\"><rect height=\"7\"/><rect height=\"8\"/></svg></div>", EngineFixture.Body(planned, new Barred([new Bar(7), new Bar(8)])));
    }

    [Fact]
    [Trait("AC", "ENGINE-183")]
    public async Task AnUnboundValueIsALiteralLikeTheEnginesAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync("""<m:Bars Points="{x:Static m:Bars.Sample}" />""", new Barred([]), new Barred([new Bar(5)]));

        Assert.DoesNotContain(planned.Plan.Body, node => node is WriterNode);
        Assert.Contains("<svg data-a=\"fill=#123\"><rect height=\"2\"/><rect height=\"9\"/></svg>", EngineFixture.Body(planned, new Barred([new Bar(5)])), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-184")]
    public async Task UnfollowedBindingsFailThePlanAsync()
    {
        var indexed = await fixture.CompileAsync("""<m:Bars Points="{Binding Groups[first]}" />""", new Barred([], Groups: ImmutableDictionary<string, ImmutableArray<Bar>>.Empty.Add("first", [new Bar(1)])));
        var captioned = await fixture.CompileAsync("""<m:Bars Points="{Binding Bars}" Caption="{Binding Title}" />""", new Barred([new Bar(1)], "t"));

        Assert.False(indexed.Reusable);
        Assert.Contains("Bars.Points", indexed.Failure, StringComparison.Ordinal);
        Assert.False(captioned.Reusable);
        Assert.Contains("Bars.Caption", captioned.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-193")]
    public async Task BoundPartsOfChildrenAreFollowedByThePlanAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(Lined, new Plotted([new Dot(1)]), new Plotted([new Dot(5), new Dot(6)], "blue"));

        Assert.Contains(planned.Plan.Body, node => node is WriterNode { Parts: [{ Read: not null }, { Read: null }] });
        Assert.Contains("<svg data-strokes=\"blue,green\" data-read=\"line0\"><rect height=\"5\"/><rect height=\"6\"/></svg>", EngineFixture.Body(planned, new Plotted([new Dot(5), new Dot(6)], "blue")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-194")]
    public async Task TheEngineHandsTheRendererTheBoundPartsAsync()
    {
        var model = new Plotted([new Dot(4)], "teal");
        var engine = await fixture.CompileAsync(Lined.Replace("<m:Plot ", "<m:Plot Caption=\"{Binding Title}\" ", StringComparison.Ordinal), model);
        var planned = await fixture.CompileAsync(Lined, model);

        Assert.False(engine.Reusable);
        Assert.Contains("Plot.Caption", engine.Failure, StringComparison.Ordinal);
        Assert.Equal("<svg data-strokes=\"teal,green\" data-read=\"line0\"><rect height=\"4\"/></svg>", Svg().Match(EngineFixture.Body(engine, model)).Value);
        Assert.Equal(Svg().Match(EngineFixture.Body(planned, model)).Value, Svg().Match(EngineFixture.Body(engine, model)).Value);
    }

    [Fact]
    [Trait("AC", "ENGINE-195")]
    public async Task PartsThePlanCannotFollowFailItAsync()
    {
        var indexed = await fixture.CompileAsync("""<m:Plot Points="{Binding Dots}"><m:Plot.Lines><m:PlotLine Stroke="{Binding Palette[red]}" /></m:Plot.Lines></m:Plot>""", new Plotted([new Dot(1)]));
        var unbound = await fixture.CompileAsync("""<m:Plot Points="{x:Static m:Bars.Sample}"><m:Plot.Lines><m:PlotLine Stroke="{Binding Color}" /></m:Plot.Lines></m:Plot>""", new Plotted([]));
        var misdeclared = await fixture.CompileAsync("""<m:Plot Points="{Binding Dots}" Misdeclared="True" />""", new Plotted([new Dot(1)]));

        Assert.False(indexed.Reusable);
        Assert.Contains("PlotLine.Stroke", indexed.Failure, StringComparison.Ordinal);
        Assert.False(unbound.Reusable);
        Assert.Contains("Plot.Points is not bound", unbound.Failure, StringComparison.Ordinal);
        Assert.False(misdeclared.Reusable);
        Assert.Contains("'Missing'", misdeclared.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-196")]
    public async Task APostedFieldIsWrittenBackAndRenderedAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1), new Dot(2), new Dot(3), new Dot(4)];
        var page = await fixture.PlansLikeAvaloniaAsync(Windowed, new Plotted(dots), new Plotted(dots, from: 3));
        var html = EngineFixture.Body(page, new Plotted(dots));
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);

        var posted = await fixture.PostAsync(Windowed, avalonia, (PlanForms.ChangeField, "0"), ("f0", "2"));
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form((PlanForms.ChangeField, "0"), ("f0", "2")), new AdapterSlotResolver());

        Assert.Contains("<input type=\"range\" min=\"0\" max=\"9\" value=\"0\" name=\"f0\" data-change><rect height=\"1\"/>", html, StringComparison.Ordinal);
        Assert.True(page.Plan.HasFields);
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal((2, 2), (avalonia.From, planned.From));
        Assert.Contains("value=\"2\" name=\"f0\" data-change><rect height=\"3\"/><rect height=\"4\"/></svg>", EngineFixture.Body(posted.Page!, avalonia), StringComparison.Ordinal);
        Assert.Contains("value=\"2\" name=\"f0\" data-change><rect height=\"3\"/><rect height=\"4\"/></svg>", EngineFixture.Body(page, planned), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-197")]
    public async Task PostedFieldsOutsideTheirNumbersAreKeptOrClampedAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1)];
        var page = await fixture.CompileAsync(Windowed, new Plotted(dots));
        (string? Value, int From)[] posts = [("x", 3), (null, 3), ("42", 9), ("-4", 0)];
        var answers = new List<(int, int)>();

        foreach (var (value, _) in posts)
        {
            var avalonia = new Plotted(dots, from: 3);
            var planned = new Plotted(dots, from: 3);
            (string, string)[] form = value is null ? [(PlanForms.ChangeField, "0")] : [(PlanForms.ChangeField, "0"), ("f0", value)];
            await fixture.PostAsync(Windowed, avalonia, form);
            await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());
            answers.Add((avalonia.From, planned.From));
        }

        var ghost = await Assert.ThrowsAnyAsync<Exception>(() => fixture.CompileAsync("""<m:Plot Points="{Binding Dots}" Ghost="True" />""", new Plotted(dots)));

        Assert.Equal(posts.Select(post => (post.From, post.From)), answers);
        Assert.Contains("'ghost'", ghost.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-198")]
    public async Task APostedCommandRunsWithItsItemAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(7), new Dot(8)];
        var page = await fixture.PlansLikeAvaloniaAsync(Picked, new Plotted(dots), new Plotted([new Dot(9)]));
        var html = EngineFixture.Body(page, new Plotted(dots));
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);

        var posted = await fixture.PostAsync(Picked, avalonia, ("f0", "0"), (PlanForms.ButtonField, "1|1"));
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(("f0", "0"), (PlanForms.ButtonField, "1|1")), new AdapterSlotResolver());

        Assert.Contains("<button type=\"submit\" name=\"_btn\" value=\"0|0\">7</button><button type=\"submit\" name=\"_btn\" value=\"1|1\">8</button>", html, StringComparison.Ordinal);
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal(["1"], avalonia.Picked);
        Assert.Equal(["1"], planned.Picked);
        Assert.Contains("value=\"1\" name=\"f0\" data-change><button type=\"submit\" name=\"_btn\" value=\"0|1\">8</button></svg>", EngineFixture.Body(posted.Page!, avalonia), StringComparison.Ordinal);
        Assert.Contains("value=\"1\" name=\"f0\" data-change><button type=\"submit\" name=\"_btn\" value=\"0|1\">8</button></svg>", EngineFixture.Body(page, planned), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-199")]
    public async Task CommandsPostedWithoutTheirButtonRunNothingAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(7), new Dot(8)];
        var page = await fixture.CompileAsync(Picked, new Plotted(dots));
        string[] posts = ["1|5", "2|0"];
        var answers = new List<(PostStatus, PostStatus?)>();
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);

        foreach (var post in posts)
        {
            answers.Add(((await fixture.PostAsync(Picked, avalonia, (PlanForms.ButtonField, post))).Status, await PlanForms.ApplyAsync(page.Plan, [planned], Form((PlanForms.ButtonField, post)), new AdapterSlotResolver())));
        }

        Assert.Equal([(PostStatus.Conflict, PostStatus.Conflict), (PostStatus.Conflict, PostStatus.Conflict)], answers);
        Assert.Empty(avalonia.Picked);
        Assert.Empty(planned.Picked);
    }

    [Fact]
    [Trait("AC", "ENGINE-200")]
    public async Task AChoiceFieldIsOneNameOverItsOptionsAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1)];
        var page = await fixture.PlansLikeAvaloniaAsync(Moded, new Plotted(dots), new Plotted(dots) { Mode = "bars" });
        var html = EngineFixture.Body(page, new Plotted(dots));
        var fields = PlanForms.Fields(page.Plan, [new Plotted(dots)]).Fields;
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);
        (string, string)[] form = [(PlanForms.ChangeField, "1"), ("f0", "0"), ("f1", "area")];

        var posted = await fixture.PostAsync(Moded, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.Contains("name=\"f0\" data-change><input type=\"radio\" name=\"f1\" value=\"lines\" data-change checked><input type=\"radio\" name=\"f1\" value=\"bars\" data-change><button type=\"submit\" name=\"f1\" value=\"area\" data-change>Area</button><rect height=\"1\"/>", html, StringComparison.Ordinal);
        Assert.Equal(["number:From:", "choice:Mode:lines,bars,area"], fields.Select(field => $"{field.Field!.Kind}:{field.Field.Path}:{(field.Options.IsDefault ? string.Empty : string.Join(',', field.Options))}"));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal(("area", "area"), (avalonia.Mode, planned.Mode));
        Assert.Contains("value=\"area\" data-change aria-pressed=\"true\">Area</button>", EngineFixture.Body(posted.Page!, avalonia), StringComparison.Ordinal);
        Assert.Contains("value=\"area\" data-change aria-pressed=\"true\">Area</button>", EngineFixture.Body(page, planned), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-201")]
    public async Task ChoicesTakeOnlyTheirOptionsAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1)];
        var page = await fixture.CompileAsync(Moded, new Plotted(dots));
        var oneWay = await fixture.CompileAsync(Moded.Replace("{Binding Mode}", "{Binding Mode, Mode=OneWay}", StringComparison.Ordinal), new Plotted(dots));
        (string, string)[][] posts = [[(PlanForms.ChangeField, "1"), ("f1", "pie")], [(PlanForms.ChangeField, "1")]];
        var kept = new List<(string, string)>();

        foreach (var form in posts)
        {
            var avalonia = new Plotted(dots);
            var planned = new Plotted(dots);
            await fixture.PostAsync(Moded, avalonia, form);
            await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());
            kept.Add((avalonia.Mode, planned.Mode));
        }

        Assert.Equal([("lines", "lines"), ("lines", "lines")], kept);
        Assert.True(oneWay.Plan.HasFields);
        Assert.Null(await PlanForms.ApplyAsync(oneWay.Plan, [new Plotted(dots)], Form(posts[0]), new AdapterSlotResolver()));
    }

    [Fact]
    [Trait("AC", "ENGINE-202")]
    public async Task ACompositeFieldMovesItsMembersTogetherAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1), new Dot(2), new Dot(3), new Dot(4), new Dot(5), new Dot(6), new Dot(7)];
        var page = await fixture.PlansLikeAvaloniaAsync(Slid, new Plotted(dots, from: 1) { To = 3 }, new Plotted(dots, from: 2) { To = 5 });
        var composite = PlanForms.Fields(page.Plan, [new Plotted(dots, from: 1) { To = 3 }]).Fields[1].Field!;
        var avalonia = new Plotted(dots, from: 1) { To = 3 };
        var planned = new Plotted(dots, from: 1) { To = 3 };
        (string, string)[] form = [(PlanForms.ChangeField, "1"), ("f0", "1"), ("f1", "4")];

        var posted = await fixture.PostAsync(Slid, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.Equal((CompositeFields.Kind, "slide"), (composite.Kind, CompositeFields.Key(composite)));
        Assert.Equal(["number:From", "number:To"], CompositeFields.Members(composite).Select(member => $"{member.Kind}:{member.Path}"));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal(((4, 6), (4, 6)), ((avalonia.From, avalonia.To), (planned.From, planned.To)));
        Assert.Contains("data-to=\"6\" value=\"4\" name=\"f1\" data-change><rect height=\"5\"/>", EngineFixture.Body(posted.Page!, avalonia), StringComparison.Ordinal);
        Assert.Contains("data-to=\"6\" value=\"4\" name=\"f1\" data-change><rect height=\"5\"/>", EngineFixture.Body(page, planned), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-203")]
    public async Task CompositesRefusedOrOutOfRangeAreKeptOrClampedAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1)];
        var page = await fixture.CompileAsync(Slid, new Plotted(dots, from: 1) { To = 3 });
        var oneWay = await fixture.CompileAsync(Slid.Replace("{Binding To}", "{Binding To, Mode=OneWay}", StringComparison.Ordinal), new Plotted(dots, from: 1) { To = 3 });
        (string Slide, int From, int To)[] posts = [("x", 1, 3), ("8", 8, 9)];
        var answers = new List<((int, int), (int, int))>();

        foreach (var (slide, _, _) in posts)
        {
            var avalonia = new Plotted(dots, from: 1) { To = 3 };
            var planned = new Plotted(dots, from: 1) { To = 3 };
            (string, string)[] form = [(PlanForms.ChangeField, "1"), ("f1", slide)];
            await fixture.PostAsync(Slid, avalonia, form);
            await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());
            answers.Add(((avalonia.From, avalonia.To), (planned.From, planned.To)));
        }

        var misdeclared = await Assert.ThrowsAnyAsync<Exception>(() => fixture.CompileAsync(Slid.Replace("from,to", "from,missing", StringComparison.Ordinal), new Plotted(dots)));

        Assert.Equal(posts.Select(post => ((post.From, post.To), (post.From, post.To))), answers);
        Assert.Contains("'slide'", misdeclared.ToString(), StringComparison.Ordinal);
        Assert.True(oneWay.Plan.HasFields);
        Assert.Null(await PlanForms.ApplyAsync(oneWay.Plan, [new Plotted(dots, from: 1) { To = 3 }], Form((PlanForms.ChangeField, "1"), ("f1", "2")), new AdapterSlotResolver()));
    }

    [Fact]
    [Trait("AC", "ENGINE-204")]
    public async Task AButtonTheBoundPostWritesRunsAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(7), new Dot(8), new Dot(9)];
        var page = await fixture.CompileAsync(Picked, new Plotted(dots));
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);
        (string, string)[] form = [("f0", "2"), (PlanForms.ButtonField, "0|2")];

        var posted = await fixture.PostAsync(Picked, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.Contains("<button type=\"submit\" name=\"_btn\" value=\"0|0\">7</button>", EngineFixture.Body(page, new Plotted(dots)), StringComparison.Ordinal);
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal(["2"], avalonia.Picked);
        Assert.Equal(["2"], planned.Picked);
    }

    [Fact]
    [Trait("AC", "ENGINE-205")]
    public async Task AButtonNoRenderWritesIsAConflictAfterBindingAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(7), new Dot(8), new Dot(9)];
        var page = await fixture.CompileAsync(Picked, new Plotted(dots));
        string[] buttons = ["1|2", "0|5"];
        var answers = new List<(PostStatus, PostStatus?)>();
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);

        foreach (var button in buttons)
        {
            (string, string)[] form = [("f0", "2"), (PlanForms.ButtonField, button)];
            answers.Add(((await fixture.PostAsync(Picked, avalonia, form)).Status, await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver())));
        }

        Assert.Equal([(PostStatus.Conflict, PostStatus.Conflict), (PostStatus.Conflict, PostStatus.Conflict)], answers);
        Assert.Empty(avalonia.Picked);
        Assert.Empty(planned.Picked);
    }

    [Fact]
    [Trait("AC", "ENGINE-206")]
    public async Task ARefusedPostLeavesTheBoundFieldsAsTheyWereAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(7), new Dot(8), new Dot(9)];
        var page = await fixture.CompileAsync(Picked, new Plotted(dots));
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);
        (string, string)[] form = [("f0", "2"), (PlanForms.ButtonField, "1|2")];

        var posted = await fixture.PostAsync(Picked, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.Equal((PostStatus.Conflict, PostStatus.Conflict), (posted.Status, applied));
        Assert.Equal((0, 0), (avalonia.From, planned.From));
        Assert.Empty(avalonia.Picked);
        Assert.Empty(planned.Picked);
    }

    [Fact]
    [Trait("AC", "ENGINE-208")]
    public async Task ANumberFieldIsClampedToItsPropertysTypeAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(7)];
        var page = await fixture.CompileAsync(Leveled, new Plotted(dots));
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);
        (string, string)[] form = [(PlanForms.ChangeField, "1"), ("f0", "0"), ("f1", "300")];

        var posted = await fixture.PostAsync(Leveled, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal((255, 255), (avalonia.Level, planned.Level));
    }

    [Fact]
    [Trait("AC", "ENGINE-209")]
    public async Task AStaleCompositeBesideTheChangedFieldIsNotDecodedAsync()
    {
        ImmutableArray<Dot> dots = [new Dot(1), new Dot(2), new Dot(3), new Dot(4), new Dot(5)];
        var page = await fixture.CompileAsync(Slid, new Plotted(dots, from: 1) { To = 3 });
        var avalonia = new Plotted(dots, from: 1) { To = 3 };
        var planned = new Plotted(dots, from: 1) { To = 3 };
        (string, string)[] form = [(PlanForms.ChangeField, "0"), ("f0", "3"), ("f1", "1")];

        var posted = await fixture.PostAsync(Slid, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal(((3, 3), (3, 3)), ((avalonia.From, avalonia.To), (planned.From, planned.To)));
    }

    [Fact]
    [Trait("AC", "ENGINE-210")]
    public async Task ButtonsAreNumberedApartFromFieldsAsync()
    {
        const string Listed = "<StackPanel>" + Picked + """<TextBox Text="{Binding Title}" /></StackPanel>""";
        ImmutableArray<Dot> dots = [new Dot(7), new Dot(8), new Dot(9)];
        var page = await fixture.CompileAsync(Listed, new Plotted(dots));
        var avalonia = new Plotted(dots);
        var planned = new Plotted(dots);
        (string, string)[] form = [("f0", "2"), ("f1", "renamed"), (PlanForms.ButtonField, "0|2")];
        (string, string)[] forged = [("f0", "2"), ("f1", "forged"), (PlanForms.ButtonField, "1|2")];

        var names = (TextName(EngineFixture.Body(page, new Plotted(dots))), TextName(EngineFixture.Body(page, new Plotted(dots, from: 2))));
        var refused = ((await fixture.PostAsync(Listed, new Plotted(dots), forged)).Status, await PlanForms.ApplyAsync(page.Plan, [new Plotted(dots)], Form(forged), new AdapterSlotResolver()));
        var posted = await fixture.PostAsync(Listed, avalonia, form);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], Form(form), new AdapterSlotResolver());

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("<button type=\"submit\" name=\"_btn\" value=\"2|2\">9</button>", EngineFixture.Body(page, new Plotted(dots)), StringComparison.Ordinal);
        Assert.Equal(("f1", "f1"), names);
        Assert.Equal((PostStatus.Conflict, PostStatus.Conflict), refused);
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (posted.Status, applied));
        Assert.Equal(("2", "renamed"), (string.Join(",", avalonia.Picked), avalonia.Title));
        Assert.Equal(("2", "renamed"), (string.Join(",", planned.Picked), planned.Title));
    }

    [Fact]
    [Trait("AC", "RENDERING-28")]
    public async Task KeyedStylesAreCollectedOncePerPageOnBothPathsAsync()
    {
        const string Charts = """<StackPanel><m:Plot Points="{Binding Dots}" Styled="sync-a:1" /><m:Plot Points="{Binding Dots}" Styled="sync-b:1" /><m:Plot Points="{Binding Dots}" Styled="sync-a:2" /><m:Plot Points="{Binding Dots}" Styled="sync-a:3" CAPTION /></StackPanel>""";
        var model = new Plotted([new Dot(4)]);
        var planned = await fixture.CompileAsync(Charts.Replace("CAPTION", string.Empty, StringComparison.Ordinal), model);
        var engine = await fixture.CompileAsync(Charts.Replace("CAPTION", "Caption=\"{Binding Title}\"", StringComparison.Ordinal), model);

        Assert.True(planned.Reusable, planned.Failure);
        Assert.False(engine.Reusable);
        Assert.Equal(["sync-a=.sync-a{order:1}", "sync-b=.sync-b{order:1}"], Contributed(planned, model));
        Assert.Equal(Contributed(planned, model), Contributed(engine, model));
        Assert.DoesNotContain("order:", EngineFixture.Body(planned, model), StringComparison.Ordinal);
        Assert.DoesNotContain("order:", EngineFixture.Body(engine, model), StringComparison.Ordinal);
        Assert.Equal((true, false), Needed());
        Assert.Throws<ArgumentException>(static () => new PageStyles().Add("sync a", ".a{}"));
    }

    private static List<string> Contributed(CompiledPage page, Plotted model)
    {
        var styles = new PageStyles();
        page.Plan.RenderBody(new System.Buffers.ArrayBufferWriter<byte>(), [model], styles: styles);
        return [.. styles.All.Select(static style => style.Key + "=" + style.Value)];
    }

    private static (bool Before, bool After) Needed()
    {
        var output = new HtmlOutput(new System.Buffers.ArrayBufferWriter<byte>()) { Styles = new PageStyles() };
        var before = output.NeedsStyle("sync-a");
        output.AddStyle("sync-a", ".a{}");
        return (before, output.NeedsStyle("sync-a"));
    }

    private static string TextName(string body) => Regex.Matches(body, "name=\"(f[^\"]+)\"")[^1].Groups[1].Value;

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value);

    [GeneratedRegex("<svg.*</svg>")]
    private static partial Regex Svg();
}
