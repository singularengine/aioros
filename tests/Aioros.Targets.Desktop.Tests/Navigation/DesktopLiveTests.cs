using Aioros.Functions;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Targets.Desktop.Navigation;
using Aioros.Targets.Desktop.Tests.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopLiveTests(DesktopFixture fixture)
{
    private const string Keyed = "Pages/keyed/Page.axaml";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-55")]
    public async Task InvalidatedKeysRefreshTheViewsOfALivePageAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var loads = new List<string>();
        var counts = new Dictionary<string, int>();
        LoaderDefinition Loader(string name, string? reads) => new(
            (services, _, _) =>
            {
                var count = counts[name] = counts.GetValueOrDefault(name) + 1;
                loads.Add(name + " " + count);
                if (reads is not null)
                {
                    services.GetRequiredService<IDataKeys>().Read(reads);
                }

                return ValueTask.FromResult(new LoaderOutcome(new ItemData(name + " " + count)));
            },
            static (_, data) => new Saving((ItemData)data!, null))
        {
            Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
        };
        ViewInstance View(int position, string name, string? reads) =>
            new(Keyed + ":0." + position, 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Saver), Loader = Loader(name, reads) });
        var live = new PageDefinition("/live", Keyed, Unreachable, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)], Live: true)
        {
            Loader = Loader("page", null),
            Views = [View(2, "a", "sales"), View(3, "b", "other"), View(4, "c", null)],
        };
        var about = new PageDefinition("/about", "Pages/about/Page.axaml", static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)]);
        await using var services = new ServiceCollection()
            .AddSingleton<DataVersions>()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static services => services.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var versions = services.GetRequiredService<DataVersions>();

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [live, about]);
            await navigator.NavigateAsync("/live");
            var watching = versions.Watchers;
            var (host, a, b) = (DesktopViewCommandTests.Context(window, 0), DesktopViewCommandTests.Context(window, 1), DesktopViewCommandTests.Context(window, 2));
            var refreshed = NextRefreshAsync(navigator, bound);
            versions.Invalidate("sales");
            await refreshed;
            var after = (string.Join(",", loads), DesktopViewCommandTests.Context(window, 0) == host, DesktopViewCommandTests.Context(window, 1) == a, DesktopViewCommandTests.Context(window, 2) == b, a.Label, b.Label, host.Label);
            await navigator.NavigateAsync("/about");
            var left = versions.Watchers;
            versions.Invalidate("sales");
            window.Close();
            return (watching, after, left, string.Join(",", loads));
        });

        Assert.Equal(1, result.watching);
        Assert.Equal(("page 1,a 1,b 1,c 1,a 2", true, true, true, "a 2", "b 1", "page 1"), result.after);
        Assert.Equal(0, result.left);
        Assert.Equal("page 1,a 1,b 1,c 1,a 2", result.Item4);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-84")]
    public async Task ReactivatingTheWindowWakesTheLiveReconnectionAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var reconnection = new LiveReconnection(new FakeTimeProvider(), static () => 1);
        await using var services = new ServiceCollection().AddSingleton(reconnection).BuildServiceProvider();

        var woken = await fixture.OnUiAsync(async () =>
        {
            var (window, _) = DesktopFixture.Open(services: services);
            var waiting = reconnection.WaitAsync(TimeSpan.FromSeconds(30), bound);
            var before = waiting.IsCompleted;
            window.Activate();
            await waiting.WaitAsync(bound);
            window.Close();
            return !before && waiting.IsCompletedSuccessfully;
        });

        Assert.True(woken);
    }

    /// <summary>The next <see cref="DesktopNavigator.LiveRefreshed"/>, bounded by the test's own timeout so a regression fails instead of hanging.</summary>
    internal static Task NextRefreshAsync(DesktopNavigator navigator, CancellationToken bound)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, EventArgs args)
        {
            navigator.LiveRefreshed -= Handler;
            done.TrySetResult();
        }

        navigator.LiveRefreshed += Handler;
        return done.Task.WaitAsync(bound);
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");
}
