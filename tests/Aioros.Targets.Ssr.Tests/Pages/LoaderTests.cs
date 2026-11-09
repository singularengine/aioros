using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class LoaderTests
{
    [Fact]
    [Trait("AC", "SSR-38")]
    public async Task LoadersReceiveTheRequestAndFeedTheViewModelAsync()
    {
        PageRequest? received = null;
        var http = Site.Http();
        http.Request.RouteValues["id"] = "7";
        http.Request.QueryString = new QueryString("?tab=open");
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ada")], "test"));
        http.Request.Headers.Cookie = "sidebar_state=false; Theme=dark";
        var site = new Site();

        await site.Handler.GetAsync(http, Loaded((_, request, _) =>
        {
            received = request;
            return OutcomeAsync($"Issue {request.Value("id")} for {request.User.Identity?.Name}");
        }));

        Assert.Equal(("/inbox", "7", "open", "ada"), (received?.Path, received?.RouteValues["id"], received?.Query["tab"], received?.User.Identity?.Name));
        Assert.Equal(("false", "dark", null), (received?.Cookies.GetValueOrDefault("sidebar_state"), received?.Cookies.GetValueOrDefault("Theme"), received?.Cookies.GetValueOrDefault("theme")));
        Assert.Empty(PageRequest.For("/inbox").Cookies);
        Assert.Equal("Issue 7 for ada", ((Model)site.Engine.Renders.Single().Models[^1]!).Title);
        Assert.Contains("<p>Issue 7 for ada</p>", Site.Body(http), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-39")]
    public async Task PageAndLayoutLoadersRunInParallelAsync()
    {
        var layoutStarted = new TaskCompletionSource();
        var pageStarted = new TaskCompletionSource();
        var layoutData = new TaskCompletionSource<object?>();
        var pageData = new TaskCompletionSource<object?>();
        var created = new List<string>();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable)
        {
            Loader = new LoaderDefinition((_, _, _) => StartedAsync(layoutStarted, layoutData), (_, data) => Created(created, data)),
        }])
        {
            Loader = new LoaderDefinition((_, _, _) => StartedAsync(pageStarted, pageData), (_, data) => Created(created, data)),
        };
        var site = new Site();

        var get = site.Handler.GetAsync(Site.Http(), page);
        var bothStarted = (layoutStarted.Task.IsCompleted, pageStarted.Task.IsCompleted);
        pageData.SetResult("page");
        var createdBeforeTheLayout = created.Count;
        layoutData.SetResult("layout");
        await get;

        Assert.Equal((true, true), bothStarted);
        Assert.Equal(0, createdBeforeTheLayout);
        Assert.Equal(["layout", "page"], created);
        Assert.Equal(["layout", "page"], site.Engine.Renders.Single().Models.Select(model => ((Model)model!).Title));
    }

    [Fact]
    [Trait("AC", "SSR-40")]
    public async Task PagesWithoutLoadersAllocateOnlyTheirModelsArrayAsync()
    {
        var layout = new Model("layout");
        var model = new Model("page");
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => model, [new LayoutDefinition("Pages/Layout.axaml", _ => layout)]);
        var loaded = Loaded((_, _, _) => OutcomeAsync("data"));
        var services = new ServiceCollection().BuildServiceProvider();
        var request = PageRequest.For("/inbox");
        await PageModels.LoadAsync(services, page, request, TestContext.Current.CancellationToken);
        await PageModels.LoadAsync(services, loaded, request, TestContext.Current.CancellationToken);

        var start = GC.GetAllocatedBytesForCurrentThread();
        var array = new object?[2];
        var arrayBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        var plain = PageModels.LoadAsync(services, page, request, TestContext.Current.CancellationToken);
        var plainBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        var withLoader = PageModels.LoadAsync(services, loaded, request, TestContext.Current.CancellationToken);
        var loaderBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        var current = await plain;
        var loadedCurrent = await withLoader;
        await PageLoader.RevalidateAsync(services, page, request, current, TestContext.Current.CancellationToken);
        await PageLoader.RevalidateAsync(services, loaded, request, loadedCurrent, TestContext.Current.CancellationToken);
        start = GC.GetAllocatedBytesForCurrentThread();
        var unchanged = PageLoader.RevalidateAsync(services, page, request, current, TestContext.Current.CancellationToken);
        var revalidateBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        var revalidated = PageLoader.RevalidateAsync(services, loaded, request, loadedCurrent, TestContext.Current.CancellationToken);
        var loaderRevalidateBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        var viewed = loaded with { Views = [.. Enumerable.Range(0, 5).Select(index => new ViewInstance("k" + index, 0, new ViewDefinition("Views/V.axaml", Unreachable) { Loader = loaded.Loader }))] };
        await PageModels.LoadAsync(services, viewed, request, TestContext.Current.CancellationToken);
        start = GC.GetAllocatedBytesForCurrentThread();
        var withViews = PageModels.LoadAsync(services, viewed, request, TestContext.Current.CancellationToken);
        var viewBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        await withViews;
        TestContext.Current.TestOutputHelper?.WriteLine($"array {arrayBytes} B, without loaders {plainBytes} B, with a page loader {loaderBytes} B, with five views besides {viewBytes} B ({(viewBytes - loaderBytes) / 5} B a view); revalidating without loaders {revalidateBytes} B, with a page loader {loaderRevalidateBytes} B");
        GC.KeepAlive(array);

        Assert.True(plain.IsCompletedSuccessfully && withLoader.IsCompletedSuccessfully);
        Assert.Equal([layout, model], current.Models);
        Assert.Equal(arrayBytes, plainBytes);
        Assert.True(unchanged.IsCompletedSuccessfully && revalidated.IsCompletedSuccessfully);
        Assert.Equal(0, revalidateBytes);
        Assert.Equal(current.Models, (await unchanged).Models);
        Assert.NotSame(loadedCurrent.Models[^1], (await revalidated).Models[^1]);
    }

    [Fact]
    [Trait("AC", "SSR-84")]
    public async Task KeyBookkeepingCostsNothingWithoutKeysAsync()
    {
        var plain = new ServiceCollection().BuildServiceProvider();
        var keyed = new ServiceCollection().AddSingleton(Aioros.Runtime.Functions.DataKeys.Ambient).AddSingleton(new ViewKeyTests.IssueCalls()).BuildServiceProvider();
        var request = PageRequest.For("/inbox");
        var bare = Loaded((_, _, _) => OutcomeAsync("data"));
        var touching = Loaded((services, _, _) =>
        {
            _ = services.GetService(typeof(Aioros.Functions.IDataKeys));
            _ = services.GetService(typeof(ViewKeyTests.IssueCalls));
            return OutcomeAsync("data");
        });
        var reading = Loaded((services, _, _) =>
        {
            ((Aioros.Functions.IDataKeys)services.GetService(typeof(Aioros.Functions.IDataKeys))!).Read("issues");
            return OutcomeAsync("data");
        });
        var token = TestContext.Current.CancellationToken;
        async Task<(long Bytes, LoadedPage Loaded)> MeasureAsync(IServiceProvider services, PageDefinition page)
        {
            var (least, last) = (long.MaxValue, default(LoadedPage));
            for (var run = 0; run < 8; run++)
            {
                var start = GC.GetAllocatedBytesForCurrentThread();
                var loading = PageLoader.LoadAsync(services, page, request, token);
                var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
                Assert.True(loading.IsCompletedSuccessfully);
                (least, last) = (Math.Min(least, bytes), await loading);
            }

            return (least, last);
        }

        var without = await MeasureAsync(plain, bare);
        var untouched = await MeasureAsync(keyed, bare);
        var touched = await MeasureAsync(keyed, touching);
        var read = await MeasureAsync(keyed, reading);
        TestContext.Current.TestOutputHelper?.WriteLine($"a page loader without key registrations {without.Bytes} B, with them {untouched.Bytes} B, taking IDataKeys and a keyed caller without reading {touched.Bytes} B, reading one key {read.Bytes} B");

        Assert.Equal(without.Bytes, untouched.Bytes);
        Assert.Equal(without.Bytes, touched.Bytes);
        Assert.True(touched.Loaded.Keys.IsDefaultOrEmpty);
        Assert.True(read.Bytes > touched.Bytes);
        Assert.Equal(["issues"], read.Loaded.Keys[^1]);
    }

    [Fact]
    [Trait("AC", "SSR-41")]
    public async Task LoadersThatThrowReachTheErrorPageAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var site = new Site(new SsrOptions { Error = new PageDefinition("/_error", "Pages/Error.axaml", _ => new Model("Something failed"), []) });
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options);
        await using var app = builder.Build();
        Exception? handled = null;
        app.UseExceptionHandler(errors => errors.Run(http =>
        {
            handled = http.Features.Get<IExceptionHandlerFeature>()?.Error;
            return site.Handler.ErrorAsync(http);
        }));
        var created = false;
        app.MapAiorosPages(
        [
            new PageDefinition("/broken", "Pages/broken/Page.axaml", Unreachable, [])
            {
                Loader = new LoaderDefinition((_, _, _) => ValueTask.FromException<LoaderOutcome>(new InvalidOperationException("The board is down.")), (_, _) =>
                {
                    created = true;
                    return new Model();
                }),
            },
        ]);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetTestClient().GetAsync(new Uri("/broken", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<p>Something failed</p>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("The board is down.", Assert.IsType<InvalidOperationException>(handled).Message);
        Assert.False(created);
    }

    [Fact]
    [Trait("AC", "SSR-42")]
    public async Task LoadersThatNavigateRedirectWithoutModelsAsync()
    {
        var created = new List<string>();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", _ =>
        {
            created.Add("layout");
            return new Model();
        })])
        {
            Loader = new LoaderDefinition(
                async (services, _, cancellationToken) =>
                {
                    await services.GetRequiredService<INavigator>().NavigateAsync(new NavigationRequest("/login"), cancellationToken);
                    return default;
                },
                (_, _) =>
                {
                    created.Add("page");
                    return new Model();
                }),
        };
        var site = new Site();
        var http = Site.Http();
        http.RequestServices = new ServiceCollection()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(services => services.GetRequiredService<PageNavigator>())
            .BuildServiceProvider()
            .CreateScope()
            .ServiceProvider;

        await site.Handler.GetAsync(http, page);

        Assert.Equal((302, "/login"), (http.Response.StatusCode, http.Response.Headers.Location.ToString()));
        Assert.Empty(created);
        Assert.Empty(site.Engine.Renders);
    }

    [Fact]
    [Trait("AC", "SSR-43")]
    public async Task PostsReloadThroughTheLoaderBeforeBindingAsync()
    {
        var loads = new List<string>();
        var created = new List<(string Data, Model Model)>();
        var site = new Site(fields: new FakeFields());
        var plan = new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(model => (model as Model)?.Title), new LiteralNode("</p>"), new IdNode { Field = new SlotSpec("text") }], []);
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition(
                (_, request, _) =>
                {
                    loads.Add(request.Path);
                    return OutcomeAsync("Loaded");
                },
                (_, data) =>
                {
                    var model = new Model((string)data!);
                    created.Add(((string)data!, model));
                    return model;
                }),
        };
        site.Cache.Preload([new PlannedPage("/inbox", "light", new CompiledPage(plan, string.Empty, "#fff", "#111", true))], [page], site.Options);
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Body = new MemoryStream("f0=Edited"u8.ToArray());

        await site.Handler.PostAsync(http, page);

        Assert.Equal(["/inbox"], loads);
        var (data, bound) = Assert.Single(created);
        Assert.Equal(("Loaded", "Edited"), (data, bound.Title));
        Assert.Contains("<p>Edited</p>", Site.Body(http), StringComparison.Ordinal);
    }

    private static PageDefinition Loaded(Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> load) =>
        new("/inbox", "Pages/inbox/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition(load, (_, data) => new Model((string)data!)),
        };

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    private static async ValueTask<LoaderOutcome> StartedAsync(TaskCompletionSource started, TaskCompletionSource<object?> data)
    {
        started.SetResult();
        return new LoaderOutcome(await data.Task);
    }

    private static ValueTask<LoaderOutcome> OutcomeAsync(object? data) => ValueTask.FromResult(new LoaderOutcome(data));

    private static Model Created(List<string> created, object? data)
    {
        created.Add((string)data!);
        return new Model((string)data!);
    }
}
