using Aioros.Pages;
using Aioros.Runtime.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class LoaderLimitTests
{
    [Fact]
    [Trait("AC", "SSR-90")]
    public async Task LoadersRunAtMostTheLimitAtOnceAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var byDefault = new Gates(12);
        var loading = PageLoader.LoadAsync(new ServiceCollection().BuildServiceProvider(), byDefault.Page(), PageRequest.For("/limited"), token);
        var startedAtOnce = byDefault.Started;
        var ninthBefore = byDefault.Signals[8].Task.IsCompleted;
        byDefault.Release(0);
        await byDefault.Signals[8].Task.WaitAsync(token);
        var afterOne = byDefault.Started;
        byDefault.ReleaseAll();
        var loaded = await loading;

        var two = new Gates(6);
        var twoLoading = PageLoader.LoadAsync(Limited(new LoaderLimits { PerLoad = 2 }), two.Page(), PageRequest.For("/limited"), token);
        var twoAtOnce = two.Started;
        two.ReleaseAll();
        await twoLoading;

        var shared = new Gates(8);
        var global = Limited(new LoaderLimits { Global = 3 });
        var first = PageLoader.LoadAsync(global, shared.Page(0, 4), PageRequest.For("/limited"), token);
        var second = PageLoader.LoadAsync(global, shared.Page(4, 4), PageRequest.For("/limited"), token);
        var sharedAtOnce = shared.Started;
        shared.ReleaseAll();
        await first;
        await second;

        Assert.Equal((8, false, 9), (startedAtOnce, ninthBefore, afterOne));
        Assert.Equal(8, byDefault.Most);
        Assert.Equal(Enumerable.Range(0, 12).Select(index => "view " + index), loaded.Models[1..].Select(model => ((Model)model!).Title));
        Assert.Equal((2, 2), (twoAtOnce, two.Most));
        Assert.Equal((3, 3), (sharedAtOnce, shared.Most));
        Assert.Equal(8, shared.Ended);
        Assert.Equal(await AllocatedAsync(Limited(new LoaderLimits { PerLoad = 0 })), await AllocatedAsync(new ServiceCollection().BuildServiceProvider()));
    }

    private static async Task<long> AllocatedAsync(IServiceProvider services)
    {
        var token = TestContext.Current.CancellationToken;
        var page = new Gates(8, open: true).Page();
        async Task<long> MeasureAsync(IServiceProvider services)
        {
            var least = long.MaxValue;
            for (var run = 0; run < 8; run++)
            {
                var start = GC.GetAllocatedBytesForCurrentThread();
                var loading = PageLoader.LoadAsync(services, page, PageRequest.For("/limited"), token);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - start);
                Assert.True(loading.IsCompletedSuccessfully);
                await loading;
            }

            return least;
        }

        return await MeasureAsync(services);
    }

    private static ServiceProvider Limited(LoaderLimits limits) => new ServiceCollection().AddSingleton(limits).BuildServiceProvider();

    private sealed class Gates(int count, bool open = false)
    {
        private readonly TaskCompletionSource[] _releases = [.. Enumerable.Range(0, count).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))];
        private int _running;
        private int _most;
        private int _started;
        private int _ended;

        public TaskCompletionSource[] Signals { get; } = [.. Enumerable.Range(0, count).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))];

        public int Started => Volatile.Read(ref _started);

        public int Most => Volatile.Read(ref _most);

        public int Ended => Volatile.Read(ref _ended);

        public PageDefinition Page(int from = 0, int views = -1) =>
            new("/limited", "Pages/limited/Page.axaml", _ => new Model("page"), [])
            {
                Views = [.. Enumerable.Range(from, views < 0 ? count : views).Select(index => new ViewInstance("Pages/limited/Page.axaml:" + index, 0, new ViewDefinition("Views/Gate.axaml", _ => null) { Loader = Loader(index) }))],
            };

        public void Release(int index) => _releases[index].TrySetResult();

        public void ReleaseAll()
        {
            for (var index = 0; index < _releases.Length; index++)
            {
                Release(index);
            }
        }

        private LoaderDefinition Loader(int index) => new(
            open ? (_, _, _) => ValueTask.FromResult(new LoaderOutcome("view " + index)) : (_, _, _) => RunAsync(index),
            (_, data) => new Model((string)data!));

        private async ValueTask<LoaderOutcome> RunAsync(int index)
        {
            var running = Interlocked.Increment(ref _running);
            int most;
            while ((most = Volatile.Read(ref _most)) < running && Interlocked.CompareExchange(ref _most, running, most) != most)
            {
            }

            Interlocked.Increment(ref _started);
            Signals[index].SetResult();
            await _releases[index].Task;
            Interlocked.Decrement(ref _running);
            Interlocked.Increment(ref _ended);
            return new LoaderOutcome("view " + index);
        }
    }
}
