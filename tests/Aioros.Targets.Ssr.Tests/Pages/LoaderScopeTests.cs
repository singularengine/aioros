using System.Security.Claims;
using Aioros.Navigation;
using Aioros.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class LoaderScopeTests
{
    [Fact]
    [Trait("AC", "SSR-49")]
    public async Task EachLoaderHasItsOwnScopeAsync()
    {
        await using var root = Root();
        await using var scope = root.CreateAsyncScope();
        var (layoutInside, pageInside, release) = (new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource());
        var used = new Dictionary<string, Exclusive>();
        var page = Held((services, _, _) => HoldAsync(services, "layout", layoutInside, release.Task, used), (services, _, _) => HoldAsync(services, "page", pageInside, release.Task, used));
        var site = new Site();
        var http = Site.Http();
        http.RequestServices = scope.ServiceProvider;

        var get = site.Handler.GetAsync(http, page);
        var bothInside = (layoutInside.Task.IsCompleted, pageInside.Task.IsCompleted);
        release.SetResult();
        await get;

        Assert.Equal((true, true), bothInside);
        Assert.NotSame(used["layout"], used["page"]);
        Assert.Equal(["layout", "page"], site.Engine.Renders.Single().Models.Select(model => ((Model)model!).Title));
    }

    [Fact]
    [Trait("AC", "SSR-50")]
    public async Task EachLoaderScopeIsDisposedWhenItsLoaderCompletesAsync()
    {
        await using var root = Root();
        await using var scope = root.CreateAsyncScope();
        var release = new TaskCompletionSource();
        var used = new Dictionary<string, Exclusive>();
        var completed = Held((services, _, _) => HoldAsync(services, "layout", new TaskCompletionSource(), Task.CompletedTask, used), (services, _, _) => HoldAsync(services, "page", new TaskCompletionSource(), Task.CompletedTask, used));
        var http = Site.Http();
        http.RequestServices = scope.ServiceProvider;
        await new Site().Handler.GetAsync(http, completed);
        var afterSuccess = (used["layout"].Disposed, used["page"].Disposed);
        var failing = Held(
            (services, _, _) => HoldAsync(services, "held", new TaskCompletionSource(), release.Task, used),
            (services, _, _) =>
            {
                used["failed"] = services.GetRequiredService<Exclusive>();
                return ValueTask.FromException<LoaderOutcome>(new InvalidOperationException("The board is down."));
            });

        var second = Site.Http();
        second.RequestServices = scope.ServiceProvider;
        var get = new Site().Handler.GetAsync(second, failing);
        var whileHeld = (used["held"].Disposed, used["failed"].Disposed);
        release.SetResult();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => get);

        Assert.Equal((true, true), afterSuccess);
        Assert.Equal((false, true), whileHeld);
        Assert.True(used["held"].Disposed);
        Assert.Equal("The board is down.", failure.Message);
    }

    [Fact]
    [Trait("AC", "SSR-51")]
    public async Task ViewModelsKeepTheRequestScopeAsync()
    {
        await using var root = new ServiceCollection()
            .AddScoped<Exclusive>()
            .AddHttpContextAccessor()
            .AddScoped<Runtime.Pages.PageNavigator>()
            .AddScoped<INavigator>(services => services.GetRequiredService<Runtime.Pages.PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = root.CreateAsyncScope();
        var http = Site.Http();
        http.RequestServices = scope.ServiceProvider;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ada")], "test"));
        root.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        (Exclusive Service, HttpContext? Http, string? User, INavigator Navigator)? loaded = null;
        Exclusive? viewModels = null;
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition(
                (services, request, _) =>
                {
                    loaded = (services.GetRequiredService<Exclusive>(), services.GetRequiredService<IHttpContextAccessor>().HttpContext, request.User.Identity?.Name, services.GetRequiredService<INavigator>());
                    return ValueTask.FromResult(new LoaderOutcome("data"));
                },
                (services, data) =>
                {
                    viewModels = services.GetRequiredService<Exclusive>();
                    return new Model((string)data!);
                }),
        };

        await new Site().Handler.GetAsync(http, page);

        Assert.Same(scope.ServiceProvider.GetRequiredService<Exclusive>(), viewModels);
        Assert.NotSame(viewModels, loaded?.Service);
        Assert.Equal((true, false), (loaded?.Service.Disposed, viewModels?.Disposed));
        Assert.Same(http, loaded?.Http);
        Assert.Equal("ada", loaded?.User);
        Assert.Same(scope.ServiceProvider.GetRequiredService<INavigator>(), loaded?.Navigator);
    }

    private static ServiceProvider Root() =>
        new ServiceCollection().AddScoped<Exclusive>().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static PageDefinition Held(Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> layout, Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> page) =>
        new("/inbox", "Pages/inbox/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable)
        {
            Loader = new LoaderDefinition(layout, static (_, data) => new Model((string)data!)),
        }])
        {
            Loader = new LoaderDefinition(page, static (_, data) => new Model((string)data!)),
        };

    private static async ValueTask<LoaderOutcome> HoldAsync(IServiceProvider services, string name, TaskCompletionSource inside, Task release, Dictionary<string, Exclusive> used)
    {
        var exclusive = services.GetRequiredService<Exclusive>();
        used[name] = exclusive;
        return new LoaderOutcome(await exclusive.UseAsync(async () =>
        {
            inside.SetResult();
            await release;
            return name;
        }));
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");
}

internal sealed class Exclusive : IAsyncDisposable
{
    private int _operations;

    public bool Disposed { get; private set; }

    public async Task<T> UseAsync<T>(Func<Task<T>> operation)
    {
        if (Interlocked.Increment(ref _operations) > 1)
        {
            Interlocked.Decrement(ref _operations);
            throw new InvalidOperationException("A second operation was started on this context instance before a previous operation completed.");
        }

        try
        {
            return await operation();
        }
        finally
        {
            Interlocked.Decrement(ref _operations);
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
