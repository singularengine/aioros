using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class ModalTests(EngineFixture fixture)
{
    private const string Page = """
        <StackPanel>
          <m:PromptHost Height="400" />
          <m:MarkedModal x:Name="edit" IsOpen="{Binding Open}" />
          <Button a:Web.Opens="{Binding #edit}" HotKey="Ctrl+E" Content="Edit" />
          <Button Content="Plain" />
          <Button Command="{Binding ShowCommand}" Content="Show" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "MOTION-18")]
    public async Task MarkedDialogsEnterAndLeaveAsTheirPartsAsync()
    {
        var page = await fixture.CompileAsync(Page.Replace("<m:MarkedModal x:Name=\"edit\"", "<m:MarkedModal x:Name=\"edit\" Moving=\"True\"", StringComparison.Ordinal), new Editing());
        var html = EngineFixture.Body(page, new Editing());
        var mask = Regex.Match(html, "<div class=\"pm k[0-9a-f]+ (?<leave>lv[0-9a-f]+) (?<enter>en[0-9a-f]+)\"></div>");
        var surface = Regex.Match(html, "<div class=\"c (?<leave>lv[0-9a-f]+) (?<enter>en[0-9a-f]+) k[0-9a-f]+\">");
        var entering = mask.Groups["enter"].Value;

        Assert.True(mask.Success && surface.Success, html[html.IndexOf("<dialog", StringComparison.Ordinal)..][..600]);
        Assert.Equal(entering, surface.Groups["enter"].Value);
        Assert.Contains($"@starting-style{{.{entering}.{entering}.{entering}.{entering}{{opacity:0;}}}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"#dlg1:not([open]) .{mask.Groups["leave"].Value}{{opacity:0;transition:opacity 150ms linear 0ms;}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"#dlg1:not([open]) .{surface.Groups["leave"].Value}{{opacity:0;transition:opacity 100ms linear 0ms;}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("#dlg1{transition:display 150ms allow-discrete,overlay 150ms allow-discrete}@media (prefers-reduced-motion:reduce){#dlg1{transition:none}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-287")]
    public async Task DialogsOpenFromTheStartArePlannedWithTheirEntranceAsync()
    {
        var body = Page.Replace("<m:MarkedModal x:Name=\"edit\"", "<m:MarkedModal x:Name=\"edit\" Moving=\"True\" OpensAttached=\"True\"", StringComparison.Ordinal);

        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Editing { Open = true });

        Assert.Matches("<div class=\"c lv[0-9a-f]+ en[0-9a-f]+ k[0-9a-f]+\">", EngineFixture.Body(planned, new Editing { Open = true }));
    }

    [Fact]
    [Trait("AC", "OVERLAY-18")]
    public async Task AKeyedDialogCarriesItsKeyAsync()
    {
        const string Keyed = """
            <StackPanel>
              <m:PromptHost Height="400" />
              <m:MarkedModal x:Name="palette" a:Web.ModalKey="Ctrl+K" />
              <m:MarkedModal x:Name="plain" />
            </StackPanel>
            """;
        var html = EngineFixture.Body(await fixture.CompileAsync(Keyed, new Editing()), new Editing());
        var dialogs = Regex.Matches(html, "<dialog [^>]*>").Select(static match => match.Value).ToList();

        Assert.Equal(2, dialogs.Count);
        Assert.Contains(" data-aioros-key=\"ctrl+KeyK\" aria-keyshortcuts=\"Control+K\"", dialogs[0], StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-key", dialogs[1], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "OVERLAY-10")]
    public async Task MarkedDialogsAreWrittenClosedForTheBrowserAsync()
    {
        var watched = new Editing();
        var writes = 0;
        watched.PropertyChanged += (_, change) => writes += change.PropertyName == nameof(Editing.Open) ? 1 : 0;
        var page = await fixture.PlansLikeAvaloniaAsync(Page, watched, new Editing { Open = true });
        var closed = EngineFixture.Body(page, new Editing());
        var open = EngineFixture.Body(page, new Editing { Open = true });
        var dialog = Regex.Match(closed, "<dialog [^>]*>").Value;
        var unmarked = EngineFixture.Body(await fixture.CompileAsync(Page.Replace("m:MarkedModal x:Name=\"edit\"", "m:Modal", StringComparison.Ordinal).Replace(" a:Web.Opens=\"{Binding #edit}\"", string.Empty, StringComparison.Ordinal), new Opening()), new Opening());

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(0, writes);
        Assert.Single(Regex.Matches(closed, "<dialog "));
        Assert.Matches("^<dialog id=\"dlg1\" class=\"prompt\" data-aioros-modal=\"_m0\" data-aioros-dismiss data-change data-aioros-known aria-labelledby=\"dlg1-title\" aria-describedby=\"dlg1-description\" style=\"position-anchor:--dh1\">$", dialog);
        Assert.Matches("<dialog id=\"dlg1\" class=\"prompt\" data-aioros-modal=\"_m0\" data-aioros-dismiss data-change data-aioros-known open ", open);
        Assert.Contains("<input type=\"checkbox\" hidden name=\"f0\"><fieldset class=\"mf\" disabled>", closed, StringComparison.Ordinal);
        Assert.Contains("<input type=\"checkbox\" hidden name=\"f0\" checked><fieldset class=\"mf\">", open, StringComparison.Ordinal);
        Assert.Matches("<div id=\"dlg1-title\"[^>]*>Edit profile</div>", closed);
        Assert.Matches("<button type=\"button\" commandfor=\"dlg1\" command=\"show-modal\"[^>]* data-aioros-key=\"[^\"]+\"[^>]*>(<[^>]+>)*Edit<", closed);
        Assert.Matches("<button type=\"button\" commandfor=\"dlg1\" command=\"close\"[^>]*>(<[^>]+>)*Close<", closed);
        Assert.Matches("name=\"_btn\" value=\"1\\|\"[^>]*>(<[^>]+>)*Plain<", closed);
        Assert.Matches("anchor-name:--dh1;", page.Css);
        Assert.Contains("<input type=\"hidden\" name=\"_m0\" value=\"False\">", closed, StringComparison.Ordinal);
        Assert.DoesNotContain("_prompt", closed, StringComparison.Ordinal);
        Assert.DoesNotContain("<dialog", unmarked, StringComparison.Ordinal);
        Assert.DoesNotContain("_m0", unmarked, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "OVERLAY-11")]
    public async Task APostFromAnOpenDialogFindsItsButtonsAsync()
    {
        var closed = EngineFixture.Body(await fixture.CompileAsync(Page, new Editing()), new Editing());
        var save = Regex.Match(closed, "name=\"_btn\" value=\"([^\"]+)\"[^>]*>(<[^>]+>)*Save<").Groups[1].Value;
        var show = Regex.Match(closed, "name=\"_btn\" value=\"([^\"]+)\"[^>]*>(<[^>]+>)*Show<").Groups[1].Value;
        var browser = new Editing();
        var saved = await fixture.PostAsync(Page, browser, ("_m0", "True"), ("f0", "on"), ("f1", "Grace"), ("_btn", save));
        var commanded = new Editing();
        var shown = await fixture.PostAsync(Page, commanded, ("_m0", "False"), ("_btn", show));
        var shownHtml = EngineFixture.Body(shown.Page!, commanded);
        var replay = System.Net.WebUtility.HtmlDecode(Regex.Match(shownHtml, "name=\"_prompt\" value=\"([^\"]*)\"").Groups[1].Value);
        var after = new Editing();
        var again = await fixture.PostAsync(Page, after, ("_prompt", replay), ("_m0", "True"), ("f0", "on"), ("_btn", save));
        var stale = new Editing();
        var refused = await fixture.PostAsync(Page, stale, ("_m0", "Sideways"), ("_btn", save));
        var changed = new Editing();
        await fixture.PostAsync(Page, changed, ("_m0", "True"), ("f0", "on"), ("_chg", "0"));

        Assert.Equal((PostStatus.Rendered, 1, false, "Grace"), (saved.Status, browser.Saved, browser.Open, browser.Name));
        Assert.DoesNotContain(" open ", Regex.Match(EngineFixture.Body(saved.Page!, browser), "<dialog [^>]*>").Value, StringComparison.Ordinal);
        Assert.Matches("<dialog id=\"dlg1\"[^>]* open ", shownHtml);
        Assert.Single(Regex.Matches(shownHtml, "<dialog "));
        Assert.Equal($"_btn={Uri.EscapeDataString(show)}", replay);
        Assert.Equal((PostStatus.Rendered, 1, false), (again.Status, after.Saved, after.Open));
        Assert.Equal((PostStatus.Conflict, 0), (refused.Status, stale.Saved));
        Assert.True(changed.Open);
    }

    [Fact]
    [Trait("AC", "OVERLAY-14")]
    public async Task OnlyTheOpenDialogsControlsResumeItWithoutTheScriptAsync()
    {
        var closed = EngineFixture.Body(await fixture.CompileAsync(Page, new Editing()), new Editing());
        var show = Regex.Match(closed, "name=\"_btn\" value=\"([^\"]+)\"[^>]*>(<[^>]+>)*Show<").Groups[1].Value;
        var shown = await fixture.PostAsync(Page, new Editing(), ("_m0", "False"), ("_btn", show));
        var html = EngineFixture.Body(shown.Page!, new Editing());
        var save = Regex.Match(html, "name=\"_btn\" value=\"([^\"]+)\"[^>]*>(<[^>]+>)*Save<").Groups[1].Value;
        var plain = Regex.Match(html, "name=\"_btn\" value=\"([^\"]+)\"[^>]*>(<[^>]+>)*Plain<").Groups[1].Value;
        var answers = System.Net.WebUtility.HtmlDecode(Regex.Match(html, "<input type=\"hidden\" name=\"_answers\" value=\"([^\"]*)\">").Groups[1].Value);
        var saving = new Editing();
        var saved = await NativeAsync(saving, html, save);
        var plaining = new Editing();
        var plained = await NativeAsync(plaining, html, plain);

        Assert.Equal("f0 f1 " + save.Split('|')[0], answers);
        Assert.Equal((PostStatus.Rendered, 1, 1), (saved.Status, saving.Shown, saving.Saved));
        Assert.Equal((PostStatus.Rendered, 0, 0), (plained.Status, plaining.Shown, plaining.Saved));
    }

    private Task<PostOutcome> NativeAsync(Editing model, string html, string button)
    {
        var form = PlanForms.Overlays(System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
            .Add("_prompt", System.Net.WebUtility.HtmlDecode(Regex.Match(html, "name=\"_prompt\" value=\"([^\"]*)\"").Groups[1].Value))
            .Add("_answers", System.Net.WebUtility.HtmlDecode(Regex.Match(html, "<input type=\"hidden\" name=\"_answers\" value=\"([^\"]*)\">").Groups[1].Value))
            .Add("_m0", "True").Add("f0", "on").Add("f1", "Ada").Add("_btn", button));
        return fixture.PostAsync(Page, model, [.. form.Select(pair => (pair.Key, pair.Value))]);
    }
    [Fact]
    [Trait("AC", "ENGINE-299")]
    public async Task ABoundDialogsOpenStateIsAStateNodeOfThePlanAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Page, new Editing(), new Editing { Open = true });
        var unbound = await fixture.PlansLikeAvaloniaAsync(Page.Replace(" IsOpen=\"{Binding Open}\"", string.Empty, StringComparison.Ordinal), new Editing());
        var shown = Regex.Match(EngineFixture.Body(page, new Editing { Open = true }), "<input type=\"checkbox\" hidden name=\"(f[0-9]+)\"").Groups[1].Value;
        var planned = new Editing();

        var status = await PlanForms.ApplyAsync(page.Plan, [planned], ImmutableDictionary<string, string>.Empty.Add("_m0", "True").Add(shown, "on"), new AdapterSlotResolver());
        var engine = await fixture.PostAsync(Page, new Editing(), ("_m0", "True"), (shown, "on"));

        Assert.Equal([new StateNode(0, new SlotSpec("state", "Open", "bool"))], States(page.Plan.Body));
        Assert.Empty(States(unbound.Plan.Body));
        Assert.Equal((PostStatus.Rendered, true), (status, planned.Open));
        Assert.Equal(EngineFixture.Body(engine.Page!), EngineFixture.Body(page, planned));
    }

    [Fact]
    [Trait("AC", "ENGINE-303")]
    public async Task AnOverlaysOpenStateIsItsOwnFieldAsync()
    {
        var body = Page.Replace("<m:PromptHost Height=\"400\" />", "<m:PromptHost Height=\"400\" /><m:Stepper a:Web.State=\"Step\" />", StringComparison.Ordinal);
        var html = await fixture.HtmlAsync(body, new Editing());
        var shown = Regex.Match(await fixture.HtmlAsync(body, new Editing { Open = true }), "<input type=\"checkbox\" hidden name=\"(f[0-9]+)\"").Groups[1].Value;
        var opened = new Editing();

        await fixture.PostAsync(body, opened, ("_s0", "0"), ("_m1", "True"), (shown, "on"));

        Assert.Contains("name=\"_s0\"", html, StringComparison.Ordinal);
        Assert.Contains("data-aioros-modal=\"_m1\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"_m1\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"_s1\"", html, StringComparison.Ordinal);
        Assert.True(opened.Open);
    }

    private static List<StateNode> States(ImmutableArray<IPlanNode> nodes) =>
        [.. nodes.SelectMany(node => node switch
        {
            StateNode state => [state],
            ScopeNode scope => States(scope.Body),
            ConditionNode condition => States(condition.Body),
            _ => [],
        })];
}
