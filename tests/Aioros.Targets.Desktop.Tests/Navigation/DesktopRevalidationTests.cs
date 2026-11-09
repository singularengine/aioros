using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Targets.Desktop.Navigation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopRevalidationTests(DesktopFixture fixture)
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-23")]
    public async Task SuccessfulCommandsRevalidateTheShownPageAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var services = Scoped();
        var loads = new List<(string Name, Marker Scope)>();
        var (revalidated, kept, texts, sameControls, layoutRecreated) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [SavingPage(loads)]);
            await navigator.NavigateAsync("/saving/7");
            var model = Saving.Last!;
            var (layout, page) = (Controls(window)[1], Controls(window)[2]);
            var layoutModel = layout.DataContext;
            var settled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            DesktopFixture.Click(window, "Save");
            var result = (await settled, ReferenceEquals(model, page.DataContext) && model.Saved == "Saved", DesktopFixture.Texts(window).ToList(), ReferenceEquals(layout, Controls(window)[1]) && ReferenceEquals(page, Controls(window)[2]), !ReferenceEquals(layoutModel, layout.DataContext));
            window.Close();
            return result;
        });

        Assert.True(revalidated);
        Assert.True(kept);
        Assert.Equal(["Shell", "section 2", "item 2", "Saved"], texts.Take(4));
        Assert.True(sameControls);
        Assert.True(layoutRecreated);
        Assert.Equal(["item 1", "item 2", "section 1", "section 2"], loads.Select(load => load.Name).Order(StringComparer.Ordinal));
        Assert.Equal(4, loads.Select(load => load.Scope).Distinct().Count());
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-24")]
    public async Task FailedOrNavigatingCommandsRevalidateNothingAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var services = Scoped();
        var loads = new List<(string Name, Marker Scope)>();
        var (failure, afterFailure, rejected, afterRejection, left, afterLeaving) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [SavingPage(loads), new PageDefinition("/about", "Pages/about/Page.axaml", static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])]);
            await navigator.NavigateAsync("/saving/7");
            var failed = DesktopNavigatorTests.NextFailureAsync(navigator, bound);
            DesktopFixture.Click(window, "Break");
            var exception = await failed;
            var broken = loads.Count;
            await navigator.NavigateAsync("/saving/7");
            var model = Saving.Last!;
            var settled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            DesktopFixture.Click(window, "Reject");
            var revalidated = await settled;
            var invalid = (revalidated, model.HasErrors && ReferenceEquals(model, Controls(window)[2].DataContext));
            var countRejected = loads.Count;
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "Leave");
            var path = await navigated;
            var result = (exception, broken, invalid, countRejected, path, loads.Count);
            window.Close();
            return result;
        });

        Assert.Equal("The save failed.", failure.Message);
        Assert.Equal(2, afterFailure);
        Assert.Equal((false, true), rejected);
        Assert.Equal(4, afterRejection);
        Assert.Equal("/about", left);
        Assert.Equal(4, afterLeaving);
    }

    [Fact]
    [Trait("AC", "DESKTOP-25")]
    public async Task KeptLayoutsTakeNewDataUnlessTheirRouteValuesChangeAsync()
    {
        var created = new List<string>();
        var loads = 0;
        string Next(string team) => "team " + team + " #" + ++loads;
        var (sameTeam, otherTeam, names) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [TeamPage("/teams/{team}/a", "Pages/about/Page.axaml", created, Next), TeamPage("/teams/{team}/b", "Pages/sections/one/Page.axaml", created, Next)]);
            await navigator.NavigateAsync("/teams/1/a");
            var first = Controls(window)[1].DataContext;
            await navigator.NavigateAsync("/teams/1/b");
            var kept = ReferenceEquals(first, Controls(window)[1].DataContext);
            var shown = DesktopFixture.Texts(window).First(text => text.StartsWith("team", StringComparison.Ordinal));
            await navigator.NavigateAsync("/teams/2/b");
            var result = (kept, !ReferenceEquals(first, Controls(window)[1].DataContext), (shown, ((Team)Controls(window)[1].DataContext!).Name));
            window.Close();
            return result;
        });

        Assert.True(sameTeam);
        Assert.True(otherTeam);
        Assert.Equal(("team 1 #2", "team 2 #3"), names);
        Assert.Equal(["team 1 #1", "team 2 #3"], created);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-26")]
    public async Task TheNewestRevalidationOrNavigationWinsAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var gates = new Queue<(TaskCompletionSource Started, TaskCompletionSource<string> Data)>();
        var tokens = new List<CancellationToken>();
        var page = new PageDefinition("/saving/{id}", "Pages/saving/Page.axaml", Unreachable, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
        {
            Loader = new LoaderDefinition(
                async (_, _, cancellationToken) =>
                {
                    tokens.Add(cancellationToken);
                    if (gates.Count == 0)
                    {
                        return new LoaderOutcome(new ItemData("first"));
                    }

                    var (started, data) = gates.Dequeue();
                    started.SetResult();
                    return new LoaderOutcome(new ItemData(await data.Task));
                },
                static (services, data) => new Saving((ItemData)data!, null).Remember())
            {
                Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
            },
        };
        var about = new PageDefinition("/about", "Pages/about/Page.axaml", static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)]);
        var (older, newer, revalidation, navigation) = (Gate(), Gate(), Gate(), Gate());
        var (label, outcomes, cancelled, shown) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page, about]);
            await navigator.NavigateAsync("/saving/7");
            gates.Enqueue(older);
            gates.Enqueue(newer);
            var newerSettled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            var olderSettled = DesktopViewCommandTests.NextSettledAsync(navigator, bound, skip: 1);
            DesktopFixture.Click(window, "Save");
            await older.Started.Task.WaitAsync(bound);
            DesktopFixture.Click(window, "Save");
            await newer.Started.Task.WaitAsync(bound);
            newer.Data.SetResult("newer");
            var newest = await newerSettled;
            older.Data.SetResult("older");
            var stale = await olderSettled;
            var label = Saving.Last!.Label;
            gates.Enqueue(revalidation);
            var abandoned = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            DesktopFixture.Click(window, "Save");
            await revalidation.Started.Task.WaitAsync(bound);
            await navigator.NavigateAsync("/about");
            revalidation.Data.SetResult("late");
            var superseded = await abandoned;
            gates.Enqueue(navigation);
            var going = navigator.NavigateAsync("/saving/8");
            await navigation.Started.Task.WaitAsync(bound);
            await navigator.NavigateAsync("/about");
            navigation.Data.SetResult("too late");
            await going;
            var result = (label, (stale, newest, superseded), (tokens[1].IsCancellationRequested, tokens[2].IsCancellationRequested, tokens[3].IsCancellationRequested, tokens[4].IsCancellationRequested), (navigator.Current, DesktopFixture.Texts(window).ToList()));
            window.Close();
            return result;
        });

        Assert.Equal("newer", label);
        Assert.Equal((false, true, false), outcomes);
        Assert.Equal((true, false, true, true), cancelled);
        Assert.Equal("/about", shown.Current);
        Assert.Equal(["Shell", "About page"], shown.Item2);
    }

    private static (TaskCompletionSource Started, TaskCompletionSource<string> Data) Gate() =>
        (new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));

    private static PageDefinition SavingPage(List<(string Name, Marker Scope)> loads)
    {
        var counts = new Dictionary<string, int>();
        return new PageDefinition("/saving/{id}", "Pages/saving/Page.axaml", Unreachable, [
            new LayoutDefinition("Pages/Layout.axaml", static _ => null),
            new LayoutDefinition("Pages/sections/Layout.axaml", Unreachable)
            {
                Loader = new LoaderDefinition((provider, _, _) => RecordedAsync(provider, loads, counts, "section", static name => new SectionData(name)), static (_, data) => new Section((SectionData)data!)),
            },
        ])
        {
            Loader = new LoaderDefinition(
                (provider, _, _) => RecordedAsync(provider, loads, counts, "item", static name => new ItemData(name)),
                static (provider, data) => new Saving((ItemData)data!, provider.GetService<INavigator>()).Remember())
            {
                Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
            },
        };
    }

    private static PageDefinition TeamPage(string route, string source, List<string> created, Func<string, string> next) =>
        new(route, source, static _ => null, [
            new LayoutDefinition("Pages/Layout.axaml", static _ => null),
            new LayoutDefinition("Pages/teams/[team]/Layout.axaml", Unreachable)
            {
                Loader = new LoaderDefinition(
                    (_, request, _) => ValueTask.FromResult(new LoaderOutcome(new TeamData(next(request.Value("team")!)))),
                    (_, data) =>
                    {
                        created.Add(((TeamData)data!).Name);
                        return new Team((TeamData)data);
                    })
                {
                    Receive = static (model, data) => ((Team)model).Receive((TeamData)data!),
                },
            },
        ]);

    private static ValueTask<LoaderOutcome> RecordedAsync(IServiceProvider services, List<(string Name, Marker Scope)> loads, Dictionary<string, int> counts, string name, Func<string, object> data)
    {
        var count = counts[name] = counts.GetValueOrDefault(name) + 1;
        loads.Add((name + " " + count, services.GetRequiredService<Marker>()));
        return ValueTask.FromResult(new LoaderOutcome(data(name + " " + count)));
    }

    private static ServiceProvider Scoped() =>
        new ServiceCollection()
            .AddScoped<Marker>()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    private static List<UserControl> Controls(Window window) =>
        [.. ((Control)window.Content!).GetSelfAndLogicalDescendants().OfType<UserControl>()];

    public sealed class Marker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
