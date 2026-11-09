using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class EnginePriorityTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-280")]
    public async Task ARequestRunsBeforeQueuedBackgroundWorkAsync()
    {
        var order = new List<string>();
        var queued = await fixture.Host.InvokeAsync(() =>
        {
            var background = Enumerable.Range(1, 3).Select(index => BackgroundWork.RunAsync(() => fixture.Host.InvokeAsync(() => Ran("background " + index), CancellationToken.None))).ToList();
            var request = fixture.Host.InvokeAsync(() => Ran("request"), CancellationToken.None);
            return background.Append(request).ToList();
        }, CancellationToken.None);
        await Task.WhenAll(queued);

        Assert.Equal(["request", "background 1", "background 2", "background 3"], order);

        string Ran(string job)
        {
            order.Add(job);
            return job;
        }
    }
}
