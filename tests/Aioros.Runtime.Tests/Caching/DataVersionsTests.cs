using Aioros.Runtime.Caching;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Tests.Caching;

public sealed class DataVersionsTests
{
    [Fact(Timeout = 30_000)]
    [Trait("AC", "FUNCTIONS-25")]
    public async Task WatchersReceiveInvalidationsInBatchesAsync()
    {
        var versions = new DataVersions();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var watch = versions.WatchAsync(stop.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        new EvictingKeys(versions).Invalidate("sales");
        var first = await watch.MoveNextAsync() ? watch.Current : null;
        versions.Invalidate("issues");
        var commanded = DataKeys.Begin();
        new EvictingKeys(versions).Invalidate("issue:7");
        DataKeys.End();
        var second = await watch.MoveNextAsync() ? watch.Current : null;
        versions.InvalidateAll();
        var third = await watch.MoveNextAsync() ? watch.Current : null;
        var watching = versions.Watchers;
        await stop.CancelAsync();
        var after = versions.Watchers;
        versions.Invalidate("late");
        var more = await watch.MoveNextAsync();
        await watch.DisposeAsync();

        Assert.Equal(["sales"], first!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["issue:7", "issues"], second!.Keys.Order(StringComparer.Ordinal));
        Assert.False(first.All || second.All);
        Assert.Equal(["issue:7"], commanded.Invalidated);
        Assert.True(third!.All);
        Assert.Equal((1, 0, false), (watching, after, more));
    }
}
