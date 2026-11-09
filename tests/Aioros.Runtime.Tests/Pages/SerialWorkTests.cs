using Aioros.Runtime.Pages;

namespace Aioros.Runtime.Tests.Pages;

public sealed class SerialWorkTests
{
    [Fact]
    [Trait("AC", "PRODUCTION-10")]
    public async Task ActionsRunOneAtATimeInOrderAsync()
    {
        var queue = new SerialWork();
        var running = 0;
        var overlapped = false;
        var order = new List<int>();
        var cancellation = TestContext.Current.CancellationToken;

        var calls = Enumerable.Range(0, 200).Select(index => queue.InvokeAsync(
            () =>
            {
                overlapped |= Interlocked.Increment(ref running) > 1;
                order.Add(index);
                Interlocked.Decrement(ref running);
            },
            cancellation).AsTask()).ToList();
        var concurrent = Enumerable.Range(0, 200).Select(_ => Task.Run(() => queue.InvokeAsync(
            () =>
            {
                overlapped |= Interlocked.Increment(ref running) > 1;
                Interlocked.Decrement(ref running);
            },
            cancellation).AsTask(), cancellation)).ToList();
        var failing = queue.InvokeAsync(() => throw new InvalidOperationException("broken"), cancellation).AsTask();
        var after = queue.InvokeAsync(() => order.Add(200), cancellation).AsTask();
        await Task.WhenAll(calls);
        await Task.WhenAll(concurrent);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => failing);
        await after;

        Assert.False(overlapped);
        Assert.Equal(Enumerable.Range(0, 201), order);
        Assert.Equal("broken", error.Message);
    }
}
