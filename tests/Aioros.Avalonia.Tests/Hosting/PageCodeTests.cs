using System.Collections.Immutable;
using System.Net;
using Aioros.Avalonia.Tests.Models;
using Aioros.Avalonia.Tests.Pages;
using Microsoft.AspNetCore.TestHost;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class PageCodeTests(EngineFixture fixture)
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-111")]
    public async Task AnAsyncVoidHandlerThatThrowsFailsOnlyItsOwnRequestAsync()
    {
        await using var app = await PageServer.StartAsync(
            fixture,
            PageServer.Page("/failing", "Failing", static _ => null),
            PageServer.Page("/simple", "Simple", static _ => new Article("Still serving")));
        using var client = app.GetTestClient();

        using var failed = await client.GetAsync(new Uri("/failing", UriKind.Relative), TestContext.Current.CancellationToken);
        var shown = await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var logged = await fixture.Logs.WaitForAsync(static entry => entry.Exception?.InnerException?.Message == Failing.Message, TestContext.Current.CancellationToken);
        using var next = await client.GetAsync(new Uri("/simple", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Contains(PageServer.ErrorTitle, shown, StringComparison.Ordinal);
        Assert.Contains("/failing", logged.Exception!.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Contains("Still serving", await next.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-112")]
    public async Task PageCodeThatThrowsAfterTheAnswerIsLoggedAndContainedAsync()
    {
        await using var app = await PageServer.StartAsync(
            fixture,
            PageServer.Page("/later", "Later", static _ => null),
            PageServer.Page("/simple", "Simple", static _ => new Article("Still serving")));
        using var client = app.GetTestClient();

        using var answered = await client.GetAsync(new Uri("/later", UriKind.Relative), TestContext.Current.CancellationToken);
        ThrowsLater.Release.TrySetResult();
        var logged = await fixture.Logs.WaitForAsync(static entry => entry.Exception?.Message == ThrowsLater.Message, TestContext.Current.CancellationToken);
        using var next = await client.GetAsync(new Uri("/simple", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Contains("/later", logged.Message, StringComparison.Ordinal);
        Assert.Contains("after its answer", logged.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-113")]
    public async Task ModelsTheEngineRendersAreCreatedOnAvaloniasThreadAsync()
    {
        var created = ImmutableList<Swatch>.Empty;
        Swatch Create()
        {
            var swatch = new Swatch("Teal");
            ImmutableInterlocked.Update(ref created, static (list, added) => list.Add(added), swatch);
            return swatch;
        }

        await using var app = await PageServer.StartAsync(fixture, PageServer.Page("/swatch", "Swatch", _ => Create()));
        using var client = app.GetTestClient();

        using var first = await client.GetAsync(new Uri("/swatch", UriKind.Relative), TestContext.Current.CancellationToken);
        using var planned = await client.GetAsync(new Uri("/swatch", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains(">Teal<", await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, planned.StatusCode);
        Assert.Equal([true, false], created.Select(static swatch => swatch.OnUiThread));
    }
}
