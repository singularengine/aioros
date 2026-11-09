using Aioros.Avalonia.Tests.Models;
using Aioros.Avalonia.Tests.Views;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class ViewEngineTests(EngineFixture fixture)
{
    private const string RecentKey = "Pages/Composed/Page.axaml:0.1";
    private const string EmbeddedKey = "Pages/Composed/Page.axaml:0.2";

    private static PageDefinition Composed(string? viewError = null) => EngineFixture.Page("Composed") with
    {
        Views =
        [
            new ViewInstance(RecentKey, 0, new ViewDefinition("Views/Recent.axaml", _ => null) { Type = static () => typeof(Recent) }),
            new ViewInstance(EmbeddedKey, 0, new ViewDefinition("Pages/Simple/Page.axaml", _ => null)),
        ],
        ViewError = viewError,
    };

    private static string Region(string body, string key)
    {
        var start = body.IndexOf($"<div data-aioros-view=\"{key}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no region {key} in {body}");
        var end = body.IndexOf("<div data-aioros-view=", start + 1, StringComparison.Ordinal);
        return end < 0 ? body[start..] : body[start..end];
    }

    [Fact]
    [Trait("AC", "ENGINE-47")]
    public async Task AViewReadsItsOwnModelInsideTheHostAsync()
    {
        var page = await fixture.RenderAsync(Composed(), "light", new Article("Host one"), new Brand("Recent one"), new Article("Embedded one"));

        var body = EngineFixture.Body(page, new Article("Host two"), new Brand("Recent two"), new Article("Embedded two"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains(">Host two</", body[..body.IndexOf("<div data-aioros-view=", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains(">Recent two</", Region(body, RecentKey), StringComparison.Ordinal);
        Assert.DoesNotContain("one", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-48")]
    public async Task APageEmbeddedByItsSourceRendersFromItsSlotAsync()
    {
        var page = await fixture.RenderAsync(Composed(), "light", new Article("Host"), new Brand("Recent"), new Article("Embedded one", "First body"));

        var region = Region(EngineFixture.Body(page, new Article("Host"), new Brand("Recent"), new Article("Embedded two", "Second body")), EmbeddedKey);

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains(">Embedded two</h1>", region, StringComparison.Ordinal);
        Assert.Contains(">Second body</div>", region, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-49")]
    public async Task BindingsAcrossAViewBoundaryAreReportedAsync()
    {
        var page = EngineFixture.Page("Reaching") with
        {
            Views = [new ViewInstance("Pages/Reaching/Page.axaml:0.1", 0, new ViewDefinition("Views/Reaching.axaml", _ => null) { Type = static () => typeof(Reaching) })],
        };

        var compiled = await fixture.RenderAsync(page, "light", new Article("Host"), new Brand("View"));

        Assert.False(compiled.Reusable, compiled.Failure);
        Assert.Contains("is bound to '$parent[UserControl,1]", compiled.Failure, StringComparison.Ordinal);
        Assert.Contains("reaches across the boundary of its view into its host", compiled.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-50")]
    public async Task FailedViewsShowTheirErrorRegionAsync()
    {
        var withTemplate = await fixture.RenderAsync(Composed("Pages/ViewError.axaml"), "light", new Article("Host"), ViewState.Failed, new Article("Embedded"));
        var builtIn = await fixture.RenderAsync(Composed(), "light", new Article("Host"), ViewState.Failed, new Article("Embedded"));
        var planned = await fixture.RenderAsync(Composed("Pages/ViewError.axaml"), "light", new Article("Host"), new Brand("Recent"), new Article("Embedded"));

        var literal = Region(EngineFixture.Body(withTemplate, new Article("Host"), ViewState.Failed, new Article("Embedded")), RecentKey);
        var fromPlan = Region(EngineFixture.Body(planned, new Article("Host"), ViewState.Failed, new Article("Embedded")), RecentKey);
        var hidden = EngineFixture.Body(planned, new Article("Host"), ViewState.Hidden, new Article("Embedded"));

        Assert.False(withTemplate.Reusable);
        Assert.Contains("is failed for this request", withTemplate.Failure, StringComparison.Ordinal);
        Assert.Contains($"<div data-aioros-view=\"{RecentKey}\" data-view-error role=\"alert\"", literal, StringComparison.Ordinal);
        Assert.Contains(">Our view broke</", literal, StringComparison.Ordinal);
        Assert.Contains(">This part of the page could not be loaded.</", Region(EngineFixture.Body(builtIn, new Article("Host"), ViewState.Failed, new Article("Embedded")), RecentKey), StringComparison.Ordinal);
        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(literal, fromPlan);
        Assert.DoesNotContain(RecentKey, hidden, StringComparison.Ordinal);
        Assert.Contains(EmbeddedKey, hidden, StringComparison.Ordinal);
    }
}
