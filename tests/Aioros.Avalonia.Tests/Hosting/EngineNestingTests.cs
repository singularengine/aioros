using Aioros.Runtime.Rendering;
using Avalonia.Threading;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class EngineNestingTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-296")]
    public async Task QueuedEngineJobsNeverRunInsideAJobsPumpAsync()
    {
        var order = new List<string>();
        var queued = await fixture.Host.InvokeAsync(() =>
        {
            var background = BackgroundWork.RunAsync(() => fixture.Host.InvokeAsync(() => Ran("background"), CancellationToken.None));
            var request = fixture.Host.InvokeAsync(() => Ran("request"), CancellationToken.None);
            Dispatcher.UIThread.Post(() => Ran("avalonia"));
            Dispatcher.UIThread.RunJobs();
            Ran("running job returns");
            return new[] { background, request };
        }, CancellationToken.None);
        await Task.WhenAll(queued);

        Assert.Equal(["avalonia", "running job returns", "request", "background"], order);

        string Ran(string job)
        {
            order.Add(job);
            return job;
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-297")]
    public async Task QueuedEngineJobsNeverRunInsideAContinuationsPumpAsync()
    {
        var order = new List<string>();
        var waiting = new TaskCompletionSource();
        Task<string>? queued = null;
        var job = fixture.Host.InvokeAsync(async () =>
        {
            await waiting.Task;
            queued = fixture.Host.InvokeAsync(() => Ran("queued"), CancellationToken.None);
            Dispatcher.UIThread.Post(() => Ran("avalonia"));
            Dispatcher.UIThread.RunJobs();
            return Ran("continuation returns");
        }, CancellationToken.None);
        _ = await fixture.Host.InvokeAsync(() => Ran("while it waits"), CancellationToken.None);
        waiting.SetResult();
        await job;
        await queued!;

        Assert.Equal(["while it waits", "avalonia", "continuation returns", "queued"], order);

        string Ran(string step)
        {
            order.Add(step);
            return step;
        }
    }
    [Fact]
    [Trait("AC", "ENGINE-302")]
    public async Task AFullEngineQueueRefusesTheNextRequestAsync()
    {
        var queued = await fixture.Host.InvokeAsync(() =>
        {
            var requests = Enumerable.Range(0, 257).Select(index => fixture.Host.InvokeAsync(() => index, CancellationToken.None)).ToList();
            var background = BackgroundWork.RunAsync(() => fixture.Host.InvokeAsync(() => -1, CancellationToken.None));
            return (requests, background);
        }, CancellationToken.None);

        var refused = await Assert.ThrowsAsync<EngineBusyException>(() => queued.requests[256]);
        var ran = await Task.WhenAll(queued.requests.Take(256));

        Assert.Equal(Enumerable.Range(0, 256), ran);
        Assert.Equal(-1, await queued.background);
        Assert.NotNull(refused);
    }
}
