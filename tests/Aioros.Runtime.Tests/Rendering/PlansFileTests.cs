using System.Buffers;
using System.Text;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Tests.Rendering;

public sealed class PlansFileTests
{
    private static CompiledPage Page(string text) => new(new RenderPlan([], [new LiteralNode(text)], []), ".p{}", "#fff", "#000", Reusable: true);

    [Fact]
    [Trait("AC", "RENDERING-31")]
    public void PlannedPagesKeepTheirCulture()
    {
        PlannedPage[] pages =
        [
            new("/", "light", Page("<p>June</p>")) { Culture = "en-US" },
            new("/", "light", Page("<p>juin</p>")) { Culture = "fr-FR" },
            new("/about", "dark", Page("<p>About</p>")),
        ];

        var json = PlansFile.Write(pages).Json;
        var read = PlansFile.Read(json, new Resolver());
        var older = PlansFile.Read(json.Replace(",\"culture\":\"en-US\"", string.Empty, StringComparison.Ordinal).Replace(",\"culture\":\"fr-FR\"", string.Empty, StringComparison.Ordinal), new Resolver());

        Assert.Equal(["en-US", "fr-FR", string.Empty], read.Select(page => page.Culture));
        Assert.Equal(2, json.Split("\"culture\"").Length - 1);
        Assert.All(older, page => Assert.Equal(string.Empty, page.Culture));
    }

    [Fact]
    [Trait("AC", "PRODUCTION-34")]
    public async Task PagesLaidOutAlikeShareTheirTextAndStylesheetAsync()
    {
        static CompiledPage Laid(string content, string css) =>
            new(new RenderPlan([new LiteralNode("<nav>layout</nav>")], [new LiteralNode(content)], [new LiteralNode("<footer>layout</footer>")]), css, "#fff", "#000", Reusable: true);

        PlannedPage[] pages =
        [
            new("/", "light", Laid("<p>Home</p>", ".light{}")),
            new("/", "dark", Laid("<p>Home</p>", ".dark{}")),
            new("/about", "light", Laid("<p>About</p>", ".light{}")),
            new("/about", "dark", Laid("<p>About</p>", ".dark{}")),
        ];

        var json = Encoding.UTF8.GetBytes(PlansFile.Write(pages).Json);
        var read = await PlansFile.ReadAsync(new Trickle(json), new Resolver(), TestContext.Current.CancellationToken);

        Assert.Single(read.Select(page => page.Page.Plan.Head[0]).Distinct(ReferenceEqualityComparer.Instance));
        Assert.Single(read.Select(page => page.Page.Plan.Tail[0]).Distinct(ReferenceEqualityComparer.Instance));
        Assert.Equal(2, read.Select(page => page.Page.Plan.Body[0]).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Same(read[0].Page.Css, read[2].Page.Css);
        Assert.Same(read[1].Page.Css, read[3].Page.Css);
        Assert.Equal(pages.Select(page => Render(page.Page.Plan)), read.Select(page => Render(page.Page.Plan)));
        Assert.Equal(PlansFile.Read(json, new Resolver()).Select(page => (page.Route, page.Theme, Render(page.Page.Plan))), read.Select(page => (page.Route, page.Theme, Render(page.Page.Plan))));
    }

    private static string Render(RenderPlan plan)
    {
        var writer = new ArrayBufferWriter<byte>();
        plan.Render(writer, [new object()]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private sealed class Trickle(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 7)], cancellationToken);
    }

    private sealed class Resolver : ISlotResolver
    {
        public ValueGetter Getter(SlotSpec spec) => throw new ArgumentException(spec.Path, nameof(spec));

        public ValueFormatter? Formatter(SlotSpec? spec) => null;

        public ChoicePredicate Predicate(SlotSpec spec) => throw new ArgumentException(spec.Path, nameof(spec));
    }
}
