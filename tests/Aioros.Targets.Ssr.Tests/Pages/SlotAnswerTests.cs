using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed partial class SlotAnswerTests
{
    private static readonly string Rows = string.Concat(Enumerable.Range(1, 40).Select(row => $"<li>Row {row.ToString(CultureInfo.InvariantCulture)} around the chart</li>"));

    private static readonly RenderPlan Plan = new([], [
        new LiteralNode("<p id=\"outside\">"), new ValueNode(static model => ((Dial)model!).Outside), new LiteralNode("</p><ul>" + Rows + "</ul>"),
        new LiteralNode("<div"), SlotRegionNode.Mark, new LiteralNode(" class=\"c\">"), SlotRegionNode.Open, new LiteralNode("<svg data-from=\""), new ValueNode(static model => ((Dial)model!).From),
        new LiteralNode("\"></svg><input type=\"range\" name=\"f"), IdNode.Instance, new LiteralNode("\" data-change><button type=\"submit\" name=\"_btn\" value=\""), IdNode.Instance,
        new LiteralNode("|x\">x</button>"), SlotRegionNode.Close, new LiteralNode("</div><input name=\"f"), IdNode.Instance, new LiteralNode("\">"),
    ], []);

    [Fact]
    [Trait("AC", "SSR-167")]
    public async Task APostThatChangedOnlyItsSlotIsAnsweredWithTheRegionAsync()
    {
        var (document, hashes) = await DocumentAsync();
        var dynamics = await AnswerAsync("GET", null, Plan.Template.Fingerprint, null, Moved);
        var changed = await AnswerAsync("POST", "_chg=0&f0=5", null, hashes, Moved);
        var pressed = await AnswerAsync("POST", "_btn=1%7Cx&f0=5", null, hashes, Moved);
        var whole = await AnswerAsync("POST", "_chg=0&f0=5", null, null, Moved);
        var region = Site.Body(changed.Http);
        var page = new ArrayBufferWriter<byte>();
        Plan.RenderBody(page, [changed.Model]);

        Assert.Single(Nonced().Matches(document));
        Assert.Equal(Kept(hashes), Kept(dynamics.Http.Response.Headers[SlotAnswers.HashesHeader].ToString()));
        Assert.Equal(("text/html; charset=utf-8", "0", Kept(hashes)), (changed.Http.Response.ContentType, changed.Http.Response.Headers[SlotAnswers.Header].ToString(), Kept(changed.Http.Response.Headers[SlotAnswers.HashesHeader].ToString())));
        Assert.Equal("<svg data-from=\"5\"></svg><input type=\"range\" name=\"f0\" data-change><button type=\"submit\" name=\"_btn\" value=\"1|x\">x</button>", region);
        Assert.Contains(SlotRegions.Open + region + SlotRegions.Close, Encoding.UTF8.GetString(page.WrittenSpan), StringComparison.Ordinal);
        Assert.Equal(("0", region), (pressed.Http.Response.Headers[SlotAnswers.Header].ToString(), Site.Body(pressed.Http)));
        Assert.True(Encoding.UTF8.GetByteCount(region) * 10 < Encoding.UTF8.GetByteCount(Site.Body(whole.Http)));
        TestContext.Current.TestOutputHelper?.WriteLine($"region {Encoding.UTF8.GetByteCount(region)} B, the whole page {Encoding.UTF8.GetByteCount(Site.Body(whole.Http))} B");
    }

    [Fact]
    [Trait("AC", "SSR-168")]
    public async Task APostThatReachedBeyondItsSlotIsAnsweredWholeAsync()
    {
        var (_, hashes) = await DocumentAsync();
        var outside = await AnswerAsync("POST", "_btn=1%7Cx&f0=5", null, hashes, static (dial, _) => dial.Outside = "changed");
        var stale = await AnswerAsync("POST", "_chg=0&f0=5", null, "0000000000000000", Moved);
        var plain = await AnswerAsync("POST", "_chg=0&f0=5", null, hashes, Moved, navigation: false);
        var elsewhere = await AnswerAsync("POST", "_chg=2&f2=a", null, hashes, Moved);
        var planned = await AnswerAsync("POST", "_btn=1%7Cx&f0=5", Plan.Template.Fingerprint, hashes, static (dial, _) => dial.Outside = "changed");

        Assert.All(new[] { outside, stale, plain, elsewhere, planned }, answer => Assert.False(answer.Http.Response.Headers.ContainsKey(SlotAnswers.Header)));
        Assert.Contains("<p id=\"outside\">changed</p>", Site.Body(outside.Http), StringComparison.Ordinal);
        Assert.Contains("<title>", Site.Body(stale.Http), StringComparison.Ordinal);
        Assert.StartsWith("<!doctype html>", Site.Body(plain.Http), StringComparison.Ordinal);
        Assert.Contains("<title>", Site.Body(elsewhere.Http), StringComparison.Ordinal);
        Assert.Equal("application/json", planned.Http.Response.ContentType);
        Assert.NotEqual(Kept(hashes), Kept(planned.Http.Response.Headers[SlotAnswers.HashesHeader].ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-196")]
    public async Task ADynamicsAnswerToAPostNamesItsRegionsWithTheNonceItsHashesCarryAsync()
    {
        var planned = await AnswerAsync("POST", "_chg=0&f0=5", Plan.Template.Fingerprint, null, Moved);
        var hashes = planned.Http.Response.Headers[SlotAnswers.HashesHeader].ToString();
        var nonce = hashes[..hashes.IndexOf(':', StringComparison.Ordinal)];

        Assert.Equal("application/json", planned.Http.Response.ContentType);
        Assert.Matches("^[0-9a-f]{8}$", nonce);
        Assert.Contains("data-aioros-slot=\\\"" + nonce + "\\\"", Site.Body(planned.Http), StringComparison.Ordinal);
    }

    private static void Moved(Dial dial, ImmutableDictionary<string, string> posted)
    {
        if (posted.TryGetValue("f0", out var from))
        {
            dial.From = int.Parse(from, CultureInfo.InvariantCulture);
        }
    }

    private static async Task<(string Document, string Hashes)> DocumentAsync()
    {
        var answer = await AnswerAsync("GET", null, null, null, Moved, navigation: false);
        var document = Site.Body(answer.Http);
        return (document, SlotsMeta().Match(document).Groups[1].Value);
    }

    private static async Task<(DefaultHttpContext Http, Dial Model)> AnswerAsync(string method, string? form, string? plan, string? hashes, Action<Dial, ImmutableDictionary<string, string>> command, bool navigation = true)
    {
        var model = new Dial();
        var site = new Site();
        site.Engine.Plan = Plan;
        site.Engine.Fields = true;
        site.Engine.Command = _ =>
        {
            command(model, site.Engine.Posted!);
            return ValueTask.CompletedTask;
        };
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => model, []);
        await site.Handler.GetAsync(Site.Http(), page);
        var http = Site.Http();
        http.Request.Method = method;
        if (navigation)
        {
            http.Request.Headers[DocumentParts.NavigationHeader] = "1";
        }

        if (plan is not null)
        {
            http.Request.Headers[PageHandler.PlanHeader] = plan;
        }

        if (hashes is not null)
        {
            http.Request.Headers[SlotAnswers.HashesHeader] = hashes;
        }

        if (form is null)
        {
            await site.Handler.GetAsync(http, page);
        }
        else
        {
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
            await site.Handler.PostAsync(http, page);
        }

        return (http, model);
    }

    [GeneratedRegex("<meta name=\"aioros-slots\" content=\"([^\"]+)\">")]
    private static partial Regex SlotsMeta();

    [GeneratedRegex("data-aioros-slot=\"[0-9a-f]{8}\"")]
    private static partial Regex Nonced();

    /// <summary>The hashes of what is outside each region, without the render's nonce before them.</summary>
    private static string Kept(string hashes) => hashes[(hashes.IndexOf(':', StringComparison.Ordinal) + 1)..];

    private sealed class Dial : IPageModel
    {
        public string Title => "Dial";

        public int From { get; set; } = 1;

        public string Outside { get; set; } = "kept";
    }
}
