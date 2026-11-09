using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopLoaderTests(DesktopFixture fixture)
{
    [Fact]
    [Trait("AC", "DESKTOP-17")]
    public async Task LoadersFeedTheViewModelsAndKeptLayoutsAsync()
    {
        var (first, second, sameSection, freshModel) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/sections/loaded/3?tab=open%20notes");
            var shown = DesktopFixture.Texts(window).ToList();
            var section = Layouts(window)[1];
            var model = section.DataContext;
            await navigator.NavigateAsync("/sections/also");
            var result = (shown, DesktopFixture.Texts(window).ToList(), ReferenceEquals(section, Layouts(window)[1]), !ReferenceEquals(model, section.DataContext) && section.DataContext is Section);
            window.Close();
            return result;
        });

        Assert.Equal(["Shell", "loaded /sections/loaded/3", "loaded 3/open notes"], first);
        Assert.Equal(["Shell", "loaded /sections/also", "One"], second);
        Assert.True(sameSection);
        Assert.True(freshModel);
    }

    [Fact]
    [Trait("AC", "DESKTOP-18")]
    public async Task LoadersThatFailOrNavigateAreFollowedAsync()
    {
        using var services = DesktopFixture.Navigation();
        var (failure, shown, detoured, created) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services);
            await navigator.NavigateAsync("/");
            Exception? failed = null;
            navigator.Failed += (_, exception) => failed = exception;
            await navigator.TryNavigateAsync("/failing");
            var texts = DesktopFixture.Texts(window).ToList();
            Detour.Created = 0;
            await navigator.NavigateAsync("/detour");
            var result = (failed, texts, navigator.Current, Detour.Created);
            window.Close();
            return result;
        });

        Assert.Equal("The loader is down.", Assert.IsType<InvalidOperationException>(failure).Message);
        Assert.Equal(["Shell", "Something went wrong"], shown);
        Assert.Equal("/items/9", detoured);
        Assert.Equal(0, created);
    }

    [Fact]
    [Trait("AC", "DESKTOP-19")]
    public async Task LoaderResultsShowTheNotFoundPageOrRedirectAsync()
    {
        var (missing, moved, shown, created) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            Detour.Created = 0;
            await navigator.NavigateAsync("/missing");
            var texts = DesktopFixture.Texts(window).ToList();
            await navigator.NavigateAsync("/moved/here");
            var result = (texts, navigator.Current, DesktopFixture.Texts(window).ToList(), Detour.Created);
            window.Close();
            return result;
        });

        Assert.Equal(["Shell", "Nothing here"], missing);
        Assert.Equal("/items/5", moved);
        Assert.Equal(["Shell", "5"], shown);
        Assert.Equal(0, created);
    }

    [Fact]
    [Trait("AC", "DESKTOP-20")]
    public async Task LoaderTitlesNameTheWindowAsync()
    {
        var (titled, untitled) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/sections/titled");
            var first = window.Title;
            await navigator.NavigateAsync("/sections/untitled");
            var result = (first, window.Title);
            window.Close();
            return result;
        });

        Assert.Equal("Titled · Lumen", titled);
        Assert.Equal("Section", untitled);
    }

    [Fact]
    [Trait("AC", "DESKTOP-44")]
    public async Task StaticTitlesAndTemplatesNameTheWindowAsync()
    {
        var (declared, loaded) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/sections/static");
            var first = window.Title;
            await navigator.NavigateAsync("/sections/overridden");
            var result = (first, window.Title);
            window.Close();
            return result;
        });

        Assert.Equal("Static · Lumen", declared);
        Assert.Equal("Loaded · Lumen", loaded);
    }

    [Fact]
    [Trait("AC", "DESKTOP-21")]
    public async Task EachLoaderHasItsOwnScopeAsync()
    {
        await using var services = Scoped();
        var (layoutInside, pageInside, release) = (new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource());
        var used = new Dictionary<string, Exclusive>();
        var held = Held("/held", (provider, _, _) => HoldAsync(provider, "layout", layoutInside, release.Task, used), (provider, _, _) => HoldAsync(provider, "page", pageInside, release.Task, used));
        var failing = Held(
            "/failing",
            (provider, _, _) => HoldAsync(provider, "outer", new TaskCompletionSource(), Task.CompletedTask, used),
            (provider, _, _) =>
            {
                used["failed"] = provider.GetRequiredService<Exclusive>();
                return ValueTask.FromException<LoaderOutcome>(new InvalidOperationException("The loader is down."));
            });
        var (bothInside, whileHeld, failure) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [held, failing]);
            var navigating = navigator.NavigateAsync("/held");
            var inside = (layoutInside.Task.IsCompleted, pageInside.Task.IsCompleted);
            var disposed = (used["layout"].Disposed, used["page"].Disposed);
            release.SetResult();
            await navigating;
            Exception? failed = null;
            navigator.Failed += (_, exception) => failed = exception;
            await navigator.TryNavigateAsync("/failing");
            window.Close();
            return (inside, disposed, failed);
        });

        Assert.Equal((true, true), bothInside);
        Assert.Equal((false, false), whileHeld);
        Assert.NotSame(used["layout"], used["page"]);
        Assert.All(used.Values, exclusive => Assert.True(exclusive.Disposed));
        Assert.Equal("The loader is down.", Assert.IsType<InvalidOperationException>(failure).Message);
    }

    [Fact]
    [Trait("AC", "DESKTOP-22")]
    public async Task ViewModelsKeepThePageScopeAsync()
    {
        await using var services = Scoped();
        (Exclusive Service, INavigator Navigator)? loaded = null;
        Exclusive? viewModels = null;
        INavigator? pages = null;
        var page = new PageDefinition("/held", "Pages/about/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
        {
            Loader = new LoaderDefinition(
                (provider, _, _) =>
                {
                    loaded = (provider.GetRequiredService<Exclusive>(), provider.GetRequiredService<INavigator>());
                    return ValueTask.FromResult(new LoaderOutcome(null));
                },
                (provider, _) =>
                {
                    viewModels = provider.GetRequiredService<Exclusive>();
                    pages = provider.GetRequiredService<INavigator>();
                    return new object();
                }),
        };
        var shown = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [page]);
            await navigator.NavigateAsync("/held");
            var alive = viewModels?.Disposed == false;
            window.Close();
            return alive;
        });

        Assert.True(shown);
        Assert.NotSame(viewModels, loaded?.Service);
        Assert.True(loaded?.Service.Disposed);
        Assert.Same(pages, loaded?.Navigator);
    }

    private static ServiceProvider Scoped() =>
        new ServiceCollection()
            .AddScoped<Exclusive>()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static PageDefinition Held(string route, Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> layout, Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> page) =>
        new(route, "Pages/about/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", Unreachable)
        {
            Loader = new LoaderDefinition(layout, static (_, _) => null),
        }])
        {
            Loader = new LoaderDefinition(page, static (_, _) => new object()),
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

    private static List<UserControl> Layouts(Window window) =>
        [.. ((Control)window.Content!).GetSelfAndLogicalDescendants().OfType<UserControl>().Where(control => control.GetLogicalDescendants().OfType<Aioros.Avalonia.Controls.Outlet>().Any())];
}
