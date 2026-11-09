using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Live;
using Aioros.Targets.Ssr.Pages;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed partial class PageStyleTests
{
    [Fact]
    [Trait("AC", "SSR-193")]
    public async Task KeyedStylesAreWrittenOnceInTheHeadAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [Chart(static _ => ".a{x:1}</style>"), Chart(static _ => ".a{x:2}"), Chart(static _ => ".a{x:3}")], []);
        var page = Site.Page(static () => new Model());
        var document = Site.Http();
        var navigation = Site.Http();
        navigation.Request.Headers[DocumentParts.NavigationHeader] = "1";
        var dynamics = Site.Http();
        dynamics.Request.Headers[DocumentParts.NavigationHeader] = "1";
        dynamics.Request.Headers[PageHandler.PlanHeader] = site.Engine.Plan.Template.Fingerprint;

        await site.Handler.GetAsync(document, page);
        await site.Handler.GetAsync(navigation, page);
        await site.Handler.GetAsync(dynamics, page);
        var html = Site.Body(document);
        var head = html[..html.IndexOf("<div class=\"aioros-body\"", StringComparison.Ordinal)];
        var answered = JsonDocument.Parse(Site.Body(dynamics)).RootElement;

        Assert.Single(Keyed().Matches(html));
        Assert.Matches(Keyed(), head);
        Assert.Contains(">.a{x:1}<\\/style></style>", head, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(html, "<svg></svg>"));
        Assert.DoesNotContain(".a{x:", html[head.Length..], StringComparison.Ordinal);
        Assert.Single(Keyed().Matches(Site.Body(navigation)));
        Assert.Single(Keyed().Matches(answered.GetProperty("s").GetString()!));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-195")]
    public async Task ALiveStreamSendsAKeyedStyleOnlyInAVersionItHasNotSentAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new ValueNode(static model => ((Model)model!).Title), Chart(static model => ((Model)model!).Title is "B" or "C" ? ".a{x:2}" : ".a{x:1}")], []);
        var model = new Model("Start");
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var http = Site.Http(body: stream);
        http.RequestAborted = abort.Token;
        var token = TestContext.Current.CancellationToken;
        var pushed = new List<string>();

        var live = LiveUpdates.StreamAsync(http, Site.Page(() => model), site.Handler);
        await stream.Writes.ReadAsync(token);
        foreach (var title in (string[])["A", "B", "C"])
        {
            model.Title = title;
            pushed.Add(await stream.Writes.ReadAsync(token));
        }

        await abort.CancelAsync();
        await live;

        Assert.Equal([".a{x:1}", ".a{x:2}", string.Empty], pushed.Select(static push => Style().Match(push).Groups[1].Value));
        Assert.All(pushed, static push => Assert.EndsWith("<svg></svg>\n\n", push, StringComparison.Ordinal));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-257")]
    public async Task ALiveStreamSendsAKeyedStyleAgainInTheVersionItReplacedAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [new ValueNode(static model => ((Model)model!).Title), Chart(static model => ((Model)model!).Title is "B" ? ".a{x:2}" : ".a{x:1}")], []);
        var model = new Model("Start");
        var stream = new SignalStream();
        using var abort = new CancellationTokenSource();
        var http = Site.Http(body: stream);
        http.RequestAborted = abort.Token;
        var token = TestContext.Current.CancellationToken;
        var pushed = new List<string>();

        var live = LiveUpdates.StreamAsync(http, Site.Page(() => model), site.Handler);
        await stream.Writes.ReadAsync(token);
        foreach (var title in (string[])["A", "B", "A"])
        {
            model.Title = title;
            pushed.Add(await stream.Writes.ReadAsync(token));
        }

        await abort.CancelAsync();
        await live;

        Assert.Equal([".a{x:1}", ".a{x:2}", ".a{x:1}"], pushed.Select(static push => Style().Match(push).Groups[1].Value));
    }

    private static WriterNode Chart(Func<object?, string> css) => new(static model => model, (ref SlotOutput output) =>
    {
        if (output.NeedsStyle("sync-a"))
        {
            output.WriteStyle("sync-a", css(output.Value));
        }

        output.Buffer.Write("<svg></svg>"u8);
    });

    [GeneratedRegex("<style data-css data-aioros-style=\"sync-a:[0-9a-f]{16}\">")]
    private static partial Regex Keyed();

    [GeneratedRegex("data-aioros-style=\"sync-a:[0-9a-f]{16}\">([^<]*)</style>")]
    private static partial Regex Style();
}
