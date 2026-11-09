using System.Collections.Immutable;
using System.Text;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PlannedPostTests
{
    private static readonly RenderPlan Stated = new([], [new LiteralNode("<p>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</p>"), new StateNode(0, new SlotSpec("state", "Title", "string")), new IdNode { Field = new SlotSpec("text") }], []);

    [Fact]
    [Trait("AC", "SSR-262")]
    public async Task APostedStateIsRestoredThroughThePlanBeforeItsFieldsAsync()
    {
        var fields = new OrderedFields();
        var (site, model) = await OpenAsync(Stated, fields, owns: true);

        var status = await PostAsync(site, model, "_s0=Second&f0=Ada");

        Assert.Equal(200, status);
        Assert.Null(site.Engine.Posted);
        Assert.Equal(["state Second", "text Ada over Second"], fields.Bound);
        Assert.Equal("Ada", model.Title);
    }

    [Fact]
    [Trait("AC", "SSR-263")]
    public async Task AStateThePlanDoesNotRecordGoesToTheEngineAsync()
    {
        var unrecorded = new OrderedFields();
        var (site, model) = await OpenAsync(Stated, unrecorded, owns: true);
        var unbindable = new OrderedFields { Binds = false };
        var (other, otherModel) = await OpenAsync(Stated, unbindable, owns: true);

        await PostAsync(site, model, "_s1=Second&f0=Ada");
        await PostAsync(other, otherModel, "_s0=Second&f0=Ada");

        Assert.NotNull(site.Engine.Posted);
        Assert.NotNull(other.Engine.Posted);
        Assert.Empty(unrecorded.Bound);
        Assert.Equal(("Kept", "Kept"), (model.Title, otherModel.Title));
    }

    [Fact]
    [Trait("AC", "SSR-265")]
    public async Task AButtonWhoseCommandRunsAnywhereRunsThroughThePlanAsync()
    {
        var plan = new RenderPlan([], [new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command", "Go"), Button = true }], []);
        var posted = new List<(bool Engine, string? Redirect)>();
        foreach (var (commands, avalonia) in new[] { (ImmutableArray.Create("Go"), false), (ImmutableArray.Create("Other"), false), (ImmutableArray.Create("Go"), true) })
        {
            var (site, model) = await OpenAsync(plan, new FakeFields(), owns: true, page => page with { AnywhereCommands = commands, ModelsUseAvalonia = avalonia });

            await PostAsync(site, model, "f0=Ada&_btn=0%7Cgo", page => page with { AnywhereCommands = commands, ModelsUseAvalonia = avalonia });

            posted.Add((site.Engine.Posted is not null, model.RedirectTo));
        }

        Assert.Equal([(false, "/done"), (true, null), (true, null)], posted);
    }

    [Fact]
    [Trait("AC", "SSR-266")]
    public async Task AnUndescribedButtonOnlyMattersWhenPressedAsync()
    {
        var plan = new RenderPlan([], [new IdNode { Field = new SlotSpec("text") }, new IdNode { Button = true }, new IdNode { Field = new SlotSpec("command", "Go"), Button = true }], []);
        var answered = new List<(bool Engine, string Title, string? Redirect)>();
        foreach (var form in new[] { "f0=Ada&_btn=1%7Cgo", "f0=Ada&_chg=0", "f0=Ada&_btn=0%7C" })
        {
            var (site, model) = await OpenAsync(plan, new FakeFields(), owns: false);

            await PostAsync(site, model, form);

            answered.Add((site.Engine.Posted is not null, model.Title, model.RedirectTo));
        }

        Assert.Equal([(false, "Ada", "/done"), (false, "Ada", null), (true, "Kept", null)], answered);
    }

    [Fact]
    [Trait("AC", "SSR-267")]
    public async Task AKeptFieldLeavesThePostToThePlanUntilItIsPostedAsync()
    {
        var plan = new RenderPlan([], [new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("kept") }], []);
        var (site, model) = await OpenAsync(plan, new FakeFields(), owns: true);
        var (posted, postedModel) = await OpenAsync(plan, new FakeFields(), owns: true);

        await PostAsync(site, model, "f0=Ada");
        await PostAsync(posted, postedModel, "f0=Ada&f1=shown");

        Assert.Equal((false, "Ada"), (site.Engine.Posted is not null, model.Title));
        Assert.Equal((true, "Kept"), (posted.Engine.Posted is not null, postedModel.Title));
    }

    [Fact]
    [Trait("AC", "SSR-269")]
    public async Task AnUnlistedCommandStartsTheEngineAsync()
    {
        var plan = new RenderPlan([], [new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command", "Go"), Button = true }], []);
        var posted = new List<bool>();
        foreach (var (runs, commands) in new[] { (true, ImmutableArray<string>.Empty), (true, ImmutableArray.Create("Go")), (false, ImmutableArray<string>.Empty) })
        {
            var (site, model) = await OpenAsync(plan, new FakeFields(), owns: false, page => page with { AnywhereCommands = commands });
            site.Engine.RunsCommands = runs;

            await PostAsync(site, model, "f0=Ada&_btn=0%7Cgo", page => page with { AnywhereCommands = commands });

            posted.Add(site.Engine.Posted is not null);
        }

        Assert.Equal([true, false, false], posted);
    }

    [Fact]
    [Trait("AC", "SSR-275")]
    public async Task APostedStateMustBeSealedByTheServerAsync()
    {
        var plan = new RenderPlan([], [new LiteralNode("<input type=\"hidden\" name=\"_s0\" value=\""), new StateValueNode("_s0", [new ValueNode(scope => ((Model)scope!).Title)]), new LiteralNode("\"><input type=\"hidden\" name=\"_m1\" value=\"False\">"), new StateNode(0, new SlotSpec("state", "Title", "string")), new IdNode { Field = new SlotSpec("text") }], []);
        var seal = new Aioros.Targets.Ssr.Hosting.DataProtectionStateSeal(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var other = new Aioros.Targets.Ssr.Hosting.DataProtectionStateSeal(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        using var sealing = StateSeals.Use(seal);
        var fields = new OrderedFields();
        var (site, model) = await OpenAsync(plan, fields, owns: true);
        var page = Site.Http();
        await site.Handler.GetAsync(page, Site.Page(() => new Model("Kept")));
        var token = System.Text.RegularExpressions.Regex.Match(Site.Body(page), "name=\"_s0\" value=\"([^\"]+)\"").Groups[1].Value;

        var opened = await PostAsync(site, model, "_s0=" + Uri.EscapeDataString(token) + "&f0=Ada");
        var forged = new OrderedFields();
        var (forgedSite, forgedModel) = await OpenAsync(plan, forged, owns: true);
        var answers = new[]
        {
            await StaleAsync(forgedSite, forgedModel, "_s0=Second&f0=Ada", script: true),
            await StaleAsync(forgedSite, forgedModel, "_s0=" + Uri.EscapeDataString(other.Seal("Second")) + "&f0=Ada", script: true),
            await StaleAsync(forgedSite, forgedModel, "_s0=Second&f0=Ada", script: false),
        };

        Assert.NotEqual("Kept", token);
        Assert.Contains("name=\"_m1\" value=\"False\"", Site.Body(page), StringComparison.Ordinal);
        Assert.Equal(200, opened);
        Assert.Equal(["state Kept", "text Ada over Kept"], fields.Bound);
        Assert.Equal([(409, "/inbox?tab=2", ""), (409, "/inbox?tab=2", ""), (303, "", "/inbox?tab=2")], answers);
        Assert.Empty(forged.Bound);
        Assert.Null(forgedSite.Engine.Posted);
    }

    [Fact]
    [Trait("AC", "SSR-400")]
    public async Task APostOfAListItemNoLongerThereSendsThePageAnewAsync()
    {
        var rows = new RepeatNode(_ => (IReadOnlyList<string>)["b"], [new IdNode { Field = new SlotSpec("text") }, new IdNode { Field = new SlotSpec("command", "Go"), Button = true }]) { ItemKey = new SlotSpec("self"), ItemKeyGetter = static item => item };
        var plan = new RenderPlan([], [rows], []);
        var fields = new OrderedFields();
        static PageDefinition Anywhere(PageDefinition page) => page with { AnywhereCommands = ["Go"] };
        var (site, model) = await OpenAsync(plan, fields, owns: true, Anywhere);
        var gone = ItemKeys.Suffix(ItemKeys.Combine(null, "a"));
        var form = "f0" + Uri.EscapeDataString(gone) + "=Ada&_btn=0" + Uri.EscapeDataString(gone) + "%7Cgo";

        var answers = new[] { await StaleAsync(site, model, form, script: true, Anywhere), await StaleAsync(site, model, form, script: false, Anywhere) };

        Assert.Equal([(409, "/inbox?tab=2", ""), (303, "", "/inbox?tab=2")], answers);
        Assert.Empty(fields.Bound);
        Assert.Null(model.RedirectTo);
        Assert.Null(site.Engine.Posted);
    }

    [Fact]
    [Trait("AC", "SSR-401")]
    public void AListWithoutIdentityIsAWarningOfThePrecompilation()
    {
        var outcome = new Aioros.Targets.Ssr.Pages.WarmupOutcome("/rules", [], null) { Warnings = ["AIOROS033: the list bound to 'Rows' has fields"] };

        var report = Aioros.Targets.Ssr.Pages.PrecompileReport.Of([outcome], []);

        Assert.False(report.Failed);
        Assert.Contains("warning AIOROS033: the list bound to 'Rows' has fields (/rules)", report.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-431")]
    public async Task ASealedStateOpensOnlyUnderItsOwnNameAsync()
    {
        IReadOnlyList<Model> items = [new("a"), new("b")];
        var rows = new RepeatNode(_ => items, [new LiteralNode("<input type=\"hidden\" name=\"_s0"), ItemKeySuffixNode.Instance, new LiteralNode("\" value=\""), new StateValueNode("_s0", [new ValueNode(static scope => ((Model)scope!).Title)]), new LiteralNode("\">"), new StateNode(0, new SlotSpec("state"))]) { Listed = true, ItemKeyGetter = static scope => ((Model)scope!).Title };
        var plan = new RenderPlan([], [new LiteralNode("<input type=\"hidden\" name=\"_s1\" value=\""), new StateValueNode("_s1", [new ValueNode(static scope => ((Model)scope!).Title)]), new LiteralNode("\">"), new StateNode(1, new SlotSpec("state")), rows], []);
        var seal = new Aioros.Targets.Ssr.Hosting.DataProtectionStateSeal(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        using var sealing = StateSeals.Use(seal);
        var (site, model) = await OpenAsync(plan, new OrderedFields(), owns: true);
        var page = Site.Http();
        await site.Handler.GetAsync(page, Site.Page(() => model));
        string Token(string name) => Uri.EscapeDataString(System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(Site.Body(page), "name=\"" + name + "\" value=\"([^\"]+)\"").Groups[1].Value));
        var (first, second) = ("_s0" + ItemKeys.Suffix(ItemKeys.Combine(null, "a")), "_s0" + ItemKeys.Suffix(ItemKeys.Combine(null, "b")));

        var otherItem = await StaleAsync(site, model, first + "=" + Token(second), script: true);
        var otherName = await StaleAsync(site, model, first + "=" + Token("_s1"), script: true);
        var own = await StaleAsync(site, model, "_s1=" + Token("_s1") + "&" + first + "=" + Token(first), script: true);

        Assert.Equal(409, otherItem.Status);
        Assert.Equal(409, otherName.Status);
        Assert.NotEqual(409, own.Status);
    }

    [Fact]
    [Trait("AC", "SSR-432")]
    public async Task AFieldWithAnEmptyNameIsIgnoredAsync()
    {
        var plan = new RenderPlan([], [new IdNode { Field = new SlotSpec("text") }], []);
        var fields = new OrderedFields();
        var (site, model) = await OpenAsync(plan, fields, owns: true);

        string[] malformed = ["=x&f0=Ada", "_shape=&f0=Ada", "_shape=zzzz&f0=Ada", "_shape=00000000.99999999999&f0=Ada", "f~=x&f0=Ada", "f=x&f0=Ada", "_s=x&f0=Ada", "_chg=~~~"];

        foreach (var form in malformed)
        {
            Assert.InRange(await PostAsync(site, model, form), 200, 499);
        }
    }

    [Fact]
    [Trait("AC", "SSR-434")]
    public async Task AStalePostIsNeverSentToAnotherHostAsync()
    {
        var plan = new RenderPlan([], [new LiteralNode("<input type=\"hidden\" name=\"_s0\" value=\""), new StateValueNode("_s0", [new ValueNode(static scope => ((Model)scope!).Title)]), new LiteralNode("\">")], []);
        using var sealing = StateSeals.Use(new Aioros.Targets.Ssr.Hosting.DataProtectionStateSeal(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()));
        var (site, model) = await OpenAsync(plan, new OrderedFields(), owns: true);

        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.Path = "//evil.example/x";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("_s0=forged"));
        await site.Handler.PostAsync(http, Site.Page(() => model));

        Assert.Equal((303, "/"), (http.Response.StatusCode, http.Response.Headers.Location.ToString()));
    }

    private static async Task<(Site Site, Model Model)> OpenAsync(RenderPlan plan, IFieldResolver fields, bool owns, Func<PageDefinition, PageDefinition>? shape = null)
    {
        var site = new Site(fields: fields);
        site.Engine.Plan = plan;
        site.Engine.OwnsCommands = owns;
        var model = new Model("Kept");
        await site.Handler.GetAsync(Site.Http(), Shape(Site.Page(() => model), shape));
        return (site, model);
    }

    private static async Task<int> PostAsync(Site site, Model model, string form, Func<PageDefinition, PageDefinition>? shape = null)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        await site.Handler.PostAsync(http, Shape(Site.Page(() => model), shape));
        return http.Response.StatusCode;
    }

    private static async Task<(int Status, string Redirect, string Location)> StaleAsync(Site site, Model model, string form, bool script, Func<PageDefinition, PageDefinition>? shape = null)
    {
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.Path = "/inbox";
        http.Request.QueryString = new QueryString("?tab=2");
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        if (script)
        {
            http.Request.Headers["X-Aioros-Navigation"] = "1";
        }

        await site.Handler.PostAsync(http, Shape(Site.Page(() => model), shape));
        return (http.Response.StatusCode, http.Response.Headers["X-Aioros-Redirect"].ToString(), http.Response.Headers.Location.ToString());
    }

    private static PageDefinition Shape(PageDefinition page, Func<PageDefinition, PageDefinition>? shape) => shape is null ? page : shape(page);

    private sealed class OrderedFields : IFieldResolver
    {
        public List<string> Bound { get; } = [];

        public bool Binds { get; init; } = true;

        public FieldBinding? Binding(SlotSpec field) => field.Kind switch
        {
            "state" when Binds => (scope, value) =>
            {
                Bound.Add("state " + value);
                ((Model)scope!).Title = value!;
            },
            "text" => (scope, value) =>
            {
                Bound.Add("text " + value + " over " + ((Model)scope!).Title);
                ((Model)scope).Title = value ?? ((Model)scope).Title;
            },
            _ => null,
        };

        public FieldCommand? Command(SlotSpec field) => field.Kind == "command" ? new FieldCommand(_ => "go", (_, _) => ValueTask.CompletedTask) : null;
    }
}
