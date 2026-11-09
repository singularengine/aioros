using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class MemoTests
{
    [Fact]
    [Trait("AC", "SSR-89")]
    public async Task LoadersOfOneLoadShareAMemoizedCallAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new Store();
        var services = new ServiceCollection().AddSingleton(store).AddScoped<Scope>().AddTransient<SummaryCalls>().BuildServiceProvider();
        var request = PageRequest.For("/memo");

        var same = await PageLoader.LoadAsync(services, Page(7, 7), request, token);
        var sameRuns = store.Runs;
        var other = await PageLoader.LoadAsync(services, Page(7, 8), request, token);
        var otherRuns = store.Runs - sameRuns;
        var revalidated = await PageLoader.RevalidateAsync(services, Page(7, 7), request, same, token);
        var revalidatedRuns = store.Runs - sameRuns - otherRuns;
        var failing = Page(Store.Failing, Store.Failing, views: true);
        var failed = await PageLoader.LoadAsync(services, failing, request, token);
        var failedRuns = store.Failures;
        await PageLoader.LoadAsync(services, failing, request, token);
        var calls = services.GetRequiredService<SummaryCalls>();
        var before = store.Runs;
        await calls.SummaryAsync(7);
        await calls.SummaryAsync(7);
        var commandRuns = store.Runs - before;

        var (page, view) = ((Holder)same.Models[0]!, (Holder)same.Models[1]!);
        Assert.Equal(1, sameRuns);
        Assert.Same(page.Summary, view.Summary);
        Assert.NotSame(page.Scope, view.Scope);
        Assert.Equal(2, otherRuns);
        Assert.Equal((7, 8), (((Holder)other.Models[0]!).Summary.Id, ((Holder)other.Models[1]!).Summary.Id));
        Assert.Equal(1, revalidatedRuns);
        Assert.NotSame(page.Summary, ((Holder)revalidated.Models[0]!).Summary);
        Assert.Equal([ViewState.Failed, ViewState.Failed], failed.Models[1..]);
        Assert.Equal((1, 2), (failedRuns, store.Failures));
        Assert.Equal(2, commandRuns);
    }

    private static PageDefinition Page(int pageId, int viewId, bool views = false) =>
        new("/memo", "Pages/memo/Page.axaml", _ => null, [])
        {
            Loader = views ? null : Loader(pageId),
            Views = views
                ? [new("Pages/memo/Page.axaml:0", 0, new ViewDefinition("Views/A.axaml", _ => null) { Loader = Loader(pageId) }), new("Pages/memo/Page.axaml:1", 0, new ViewDefinition("Views/B.axaml", _ => null) { Loader = Loader(viewId) })]
                : [new("Pages/memo/Page.axaml:0", 0, new ViewDefinition("Views/A.axaml", _ => null) { Loader = Loader(viewId) })],
        };

    private static LoaderDefinition Loader(int id) => new(
        async (services, _, _) =>
        {
            var calls = (SummaryCalls)services.GetService(typeof(SummaryCalls))!;
            return new LoaderOutcome(new Holder(await calls.SummaryAsync(id), (Scope)services.GetService(typeof(Scope))!));
        },
        static (_, data) => data);

    internal sealed record Summary(int Id);

    private sealed record Holder(Summary Summary, Scope Scope);

    private sealed class Scope
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class Store
    {
        public const int Failing = 9;

        private int _runs;
        private int _failures;

        public int Runs => Volatile.Read(ref _runs);

        public int Failures => Volatile.Read(ref _failures);

        public async Task<Summary> SummaryAsync(int id)
        {
            await Task.Yield();
            Interlocked.Increment(ref _runs);
            if (id == Failing)
            {
                Interlocked.Increment(ref _failures);
                throw new InvalidOperationException("The store is down.");
            }

            return new Summary(id);
        }
    }

    /// <summary>A server function group's in-process caller as the generator writes it for <c>[Memo]</c>.</summary>
    private sealed class SummaryCalls(Store store) : IKeyedCalls
    {
        public IDataKeys? Keys { get; set; }

        public Task<Summary> SummaryAsync(int id) =>
            Keys is ICallMemo memo ? MemoSummaryAsync(memo, id) : store.SummaryAsync(id);

        private Task<Summary> MemoSummaryAsync(ICallMemo memo, int id) => memo.CallAsync("store/summary", ValueTuple.Create(id), () => store.SummaryAsync(id));
    }
}
