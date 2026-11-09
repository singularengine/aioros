using Aioros.Pages;
using Aioros.Runtime.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ModelLifecycleTests
{
    [Fact]
    [Trait("AC", "MVVM-01")]
    public async Task EveryCreatedModelIsReportedOnceBeforeTheRenderAsync()
    {
        var site = new Site();
        var lifecycle = new RecordingLifecycle(site.Engine);
        var layout = new Model("layout");
        var http = Http(lifecycle);

        await site.Handler.GetAsync(http, Site.Page(() => new Model("page"), () => layout));
        var plain = Site.Http();
        await site.Handler.GetAsync(plain, Site.Page(() => new Model("alone")));

        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => null, [new LayoutDefinition("Pages/Layout.axaml", _ => null)
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(new LoaderOutcome("fresh layout")), (_, data) => new Model((string)data!))
            {
                Receive = (model, data) => ((Model)model).Title = (string)data!,
            },
        }])
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(new LoaderOutcome("fresh page")), (_, data) => new Model((string)data!)),
        };
        var services = Http(lifecycle).RequestServices;
        var request = PageRequest.For("/inbox");
        var loaded = await PageLoader.LoadAsync(services, page, request, TestContext.Current.CancellationToken);
        var before = lifecycle.Created.Count;
        var fresh = await PageLoader.RevalidateAsync(services, page, request, loaded, TestContext.Current.CancellationToken);

        Assert.Equal([("layout", "/inbox", 0), ("page", "/inbox", 0)], lifecycle.Created.Take(2));
        Assert.Equal(["fresh layout", "fresh page"], lifecycle.Created.Skip(2).Take(before - 2).Select(created => created.Title));
        Assert.Same(loaded.Models[0], fresh.Models[0]);
        Assert.Equal(["fresh page"], lifecycle.Created.Skip(before).Select(created => created.Title));
        Assert.NotSame(loaded.Models[1], fresh.Models[1]);
        Assert.Contains("<p>alone</p>", Site.Body(plain), StringComparison.Ordinal);
    }

    private static DefaultHttpContext Http(RecordingLifecycle lifecycle)
    {
        var http = Site.Http();
        http.RequestServices = new ServiceCollection().AddSingleton<IModelLifecycle>(lifecycle).BuildServiceProvider();
        return http;
    }

    private sealed class RecordingLifecycle(FakeEngine engine) : IModelLifecycle
    {
        public List<(string Title, string Path, int Renders)> Created { get; } = [];

        public async ValueTask CreatedAsync(object model, PageRequest request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Created.Add((((Model)model).Title, request.Path, engine.Renders.Count));
        }
    }
}
