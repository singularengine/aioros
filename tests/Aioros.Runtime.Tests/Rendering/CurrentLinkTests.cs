using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class CurrentLinkTests
{
    [Fact]
    [Trait("AC", "RENDERING-14")]
    public void LinksToTheCurrentPageAreMarked()
    {
        var links = new[] { "/", "/games", "/games/12?tab=2#top", "/gamesx", "/crm", "/crm?page=2", "/crm?q=a%20b" };
        ImmutableArray<IPlanNode> body = [.. links.SelectMany(href => new IPlanNode[] { new LiteralNode("<a href=\"" + href + "\""), new CurrentNode(href), new LiteralNode(">") }),
            new LiteralNode("<a href=\"/games-exact\""), new CurrentNode("/games") { Exact = true }, new LiteralNode(">"),
            new LiteralNode("<a"), new CurrentNode(null, scope => ((Page)scope!).Home) { Source = SlotSpec.Bound("Home") }, new LiteralNode(">")];
        var plan = new RenderPlan([], body, []);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", new CompiledPage(plan, string.Empty, "#fff", "#000", true))]).Json, new Resolver()).Single().Page.Plan;

        Assert.Equal(["/games"], Current(plan, "/games/12"));
        Assert.Equal(["/games", "/games/12?tab=2#top"], Current(plan, "/games/12?x=1&tab=2"));
        Assert.Equal(["/crm"], Current(plan, "/crm"));
        Assert.Equal(["/crm", "/crm?page=2"], Current(plan, "/crm?page=2"));
        Assert.Equal(["/crm", "/crm?q=a%20b"], Current(plan, "/crm?q=a+b"));
        Assert.Equal(["/", "bound"], Current(plan, "/"));
        Assert.Equal(["/games", "/games-exact"], Current(plan, "/games/"));
        Assert.Empty(Current(plan, null));
        var all = new ArrayBufferWriter<byte>();
        plan.RenderBody(all, [new Page("/")], "/elsewhere");
        var html = Encoding.UTF8.GetString(all.WrittenSpan);
        Assert.Equal(links.Length + 2, System.Text.RegularExpressions.Regex.Count(html, " data-nav"));
        Assert.Contains("<a href=\"/games-exact\" data-nav data-exact>", html, StringComparison.Ordinal);
        Assert.Equal(Current(plan, "/games/12"), Current(saved, "/games/12"));
        Assert.Equal(Current(plan, "/games"), Current(saved, "/games"));
    }

    private static List<string> Current(RenderPlan plan, string? path)
    {
        var writer = new ArrayBufferWriter<byte>();
        plan.RenderBody(writer, [new Page("/")], path);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        return [.. html.Split('<', StringSplitOptions.RemoveEmptyEntries).Where(tag => tag.Contains("aria-current=\"page\"", StringComparison.Ordinal))
            .Select(tag => tag.Contains("href", StringComparison.Ordinal) ? tag.Split('"')[1] : "bound")];
    }

    private sealed record Page(string Home);

    private sealed class Resolver : ISlotResolver
    {
        public ValueGetter Getter(SlotSpec spec) => scope => ((Page)scope!).Home;

        public ValueFormatter? Formatter(SlotSpec? spec) => null;

        public ChoicePredicate Predicate(SlotSpec spec) => (_, _, _) => false;
    }
}
