using Aioros.Development;
using Aioros.Runtime.Events;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Themes;

namespace Aioros.Targets.Desktop.Tests.Development;

public sealed class CodeUpdatesTests
{
    [Fact]
    [Trait("AC", "SSR-284")]
    public async Task CodeEditsDropPlansAndNotifyTheBrowserAsync()
    {
        var bus = new EventBus();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var published = bus.SubscribeAsync<CodeReloaded>(stop.Token).GetAsyncEnumerator(stop.Token);
        var forwarding = CodeUpdates.ForwardAsync(bus, stop.Token);
        var cache = new PlanCache();
        var compiled = new CompiledPage(new RenderPlan([], [], []), string.Empty, "#fff", "#000", Reusable: true);
        cache.Store("/", ThemeSwitch.Light, new CachedPage(compiled, DocumentParts.For(compiled, new SsrOptions(), live: false)));

        CodeUpdates.UpdateApplication([typeof(CodeUpdatesTests)]);
        CodeUpdates.UpdateApplication(null);
        await published.MoveNextAsync();
        var named = published.Current;
        await published.MoveNextAsync();
        var unnamed = published.Current;
        DevelopmentChannel.Apply(named, cache);
        await stop.CancelAsync();

        Assert.Equal([typeof(CodeUpdatesTests).FullName!], named.Types);
        Assert.Empty(unnamed.Types);
        Assert.Equal(0, cache.Count);
        Assert.Equal("event: reload\ndata: code\n\n", DevelopmentChannel.Format(named));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => forwarding);
    }
}
