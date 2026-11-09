using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Targets.Desktop.Navigation;
using Aioros.Targets.Desktop.Tests.Views;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopViewCommandTests(DesktopFixture fixture)
{
    private const string Host = "Pages/clicks/Page.axaml";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-46")]
    public async Task AViewCommandRefreshesOnlyThatViewAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var loads = new List<string>();
        var tokens = new Dictionary<string, List<CancellationToken>>();
        var gates = new Queue<(TaskCompletionSource Started, TaskCompletionSource Open)>();
        var counts = new Dictionary<string, int>();
        LoaderDefinition Loader(string name, bool receives) => new(
            async (_, _, cancellationToken) =>
            {
                var count = counts[name] = counts.GetValueOrDefault(name) + 1;
                loads.Add(name + " " + count);
                if (!tokens.TryGetValue(name, out var seen))
                {
                    seen = [];
                    tokens[name] = seen;
                }

                seen.Add(cancellationToken);
                if (name == "second" && gates.TryDequeue(out var gate))
                {
                    gate.Started.SetResult();
                    await gate.Open.Task.WaitAsync(bound);
                }

                return new LoaderOutcome(new ItemData(name + " " + count));
            },
            static (_, data) => new Saving((ItemData)data!, null))
        {
            Receive = receives ? static (model, data) => ((Saving)model).Receive((ItemData)data!) : null,
        };
        var page = new PageDefinition("/clicks", Host, Unreachable, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
        {
            Loader = Loader("page", receives: true),
            Views =
            [
                new ViewInstance(Host + ":0.2", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Saver), Loader = Loader("first", receives: false) }),
                new ViewInstance(Host + ":0.3", 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Saver), Loader = Loader("second", receives: true) }),
            ],
        };

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page]);
            await navigator.NavigateAsync("/clicks");
            var (host, first, second) = (Context(window, 0), Context(window, 1), Context(window, 2));

            var settled = NextSettledAsync(navigator, bound);
            Press(window, "View save", 1);
            var secondSettled = await settled;
            var afterSecond = (string.Join(",", loads), Context(window, 0) == host, Context(window, 1) == first, Context(window, 2) == second, second.Label, second.Saved, first.Saved, host.Saved);

            settled = NextSettledAsync(navigator, bound);
            Press(window, "View save", 0);
            var firstSettled = await settled;
            var recreated = (Context(window, 1) != first, Context(window, 1).Label, Context(window, 2) == second);

            var (older, newer) = (Gate(), Gate());
            gates.Enqueue(older);
            gates.Enqueue(newer);
            Press(window, "View save", 1);
            await older.Started.Task.WaitAsync(bound);
            settled = NextSettledAsync(navigator, bound);
            Press(window, "View save", 0);
            var other = await settled;
            var untouched = tokens["second"][^1].IsCancellationRequested;
            var newerSettled = NextSettledAsync(navigator, bound);
            var olderSettled = NextSettledAsync(navigator, bound, skip: 1);
            Press(window, "View save", 1);
            await newer.Started.Task.WaitAsync(bound);
            var cancelled = tokens["second"][^2].IsCancellationRequested;
            newer.Open.SetResult();
            var newest = await newerSettled;
            older.Open.SetResult();
            var stale = await olderSettled;
            var label = second.Label;

            var before = loads.Count;
            settled = NextSettledAsync(navigator, bound);
            Press(window, "Page save", 0);
            var pageSettled = await settled;
            var pageLoads = loads.Count - before;
            window.Close();
            return (secondSettled, afterSecond, firstSettled, recreated, (other, untouched, cancelled, newest, stale, label), (pageSettled, pageLoads));
        });

        Assert.True(result.secondSettled);
        Assert.Equal(("page 1,first 1,second 1,second 2", true, true, true, "second 2", "Saved", string.Empty, string.Empty), result.afterSecond);
        Assert.True(result.firstSettled);
        Assert.Equal((true, "first 2", true), result.recreated);
        Assert.Equal((true, false, true, true, false, "second 4"), result.Item5);
        Assert.Equal((true, 3), result.Item6);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-48")]
    public async Task AViewCommandRefreshesTheViewsWhoseKeysItInvalidatedAsync()
    {
        const string Keyed = "Pages/keyed/Page.axaml";
        var bound = TestContext.Current.CancellationToken;
        var loads = new List<string>();
        var counts = new Dictionary<string, int>();
        var mode = "keys";
        Task ChangeAsync()
        {
            switch (mode)
            {
                case "keys":
                    DataKeys.Current?.Invalidate("issues");
                    break;
                case "unknown":
                    DataKeys.Current?.Undeclared();
                    break;
            }

            return Task.CompletedTask;
        }

        LoaderDefinition Loader(string name, string? reads, Func<Task>? change = null) => new(
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
            (_, data) => new Saving((ItemData)data!, null) { Change = change })
        {
            Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
        };
        ViewInstance View(int position, string name, string? reads, Func<Task>? change = null) =>
            new(Keyed + ":0." + position, 1, new ViewDefinition("Views/Saver.axaml", Unreachable) { Type = static () => typeof(Saver), Loader = Loader(name, reads, change) });
        var page = new PageDefinition("/keyed", Keyed, Unreachable, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
        {
            Loader = Loader("page", null),
            Views = [View(2, "a", "issues"), View(3, "b", "other"), View(4, "c", null, ChangeAsync)],
        };
        await using var services = DesktopFixture.Navigation();

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [page]);
            await navigator.NavigateAsync("/keyed");
            var (host, a, b, c) = (Context(window, 0), Context(window, 1), Context(window, 2), Context(window, 3));

            var settled = NextSettledAsync(navigator, bound);
            Press(window, "View save", 2);
            var keyed = (await settled, string.Join(",", loads), Context(window, 0) == host, Context(window, 1) == a, Context(window, 2) == b, Context(window, 3) == c, a.Label, b.Label, c.Label, host.Label);

            mode = "unknown";
            var before = loads.Count;
            settled = NextSettledAsync(navigator, bound);
            Press(window, "View save", 2);
            var unknown = (await settled, string.Join(",", loads.Skip(before)));

            mode = "none";
            before = loads.Count;
            settled = NextSettledAsync(navigator, bound);
            Press(window, "View save", 2);
            var none = (await settled, string.Join(",", loads.Skip(before)), b.Label);
            window.Close();
            return (keyed, unknown, none);
        });

        Assert.Equal((true, "page 1,a 1,b 1,c 1,a 2,c 2", true, true, true, true, "a 2", "b 1", "c 2", "page 1"), result.keyed);
        Assert.Equal((true, "page 2,a 3,b 2,c 3"), result.unknown);
        Assert.Equal((true, "c 4", "b 2"), result.none);
    }

    internal static Saving Context(Window window, int index)
    {
        DesktopFixture.Settle(window);
        return (Saving)window.GetVisualDescendants().OfType<Button>().Where(button => button.Content is "Page save" or "View save").OrderBy(button => button.Content is "View save").ElementAt(index).DataContext!;
    }

    internal static void Press(Window window, string text, int index)
    {
        DesktopFixture.Settle(window);
        var button = window.GetVisualDescendants().OfType<Button>().Where(button => Equals(button.Content, text)).ElementAt(index);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    }

    private static (TaskCompletionSource Started, TaskCompletionSource Open) Gate() =>
        (new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    /// <summary>The next <see cref="DesktopNavigator.CommandSettled"/>, bounded by the test's own timeout so a regression fails instead of hanging.</summary>
    internal static Task<bool> NextSettledAsync(DesktopNavigator navigator, CancellationToken bound, int skip = 0)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var remaining = skip;
        void Handler(object? sender, bool revalidated)
        {
            if (remaining-- > 0)
            {
                return;
            }

            navigator.CommandSettled -= Handler;
            done.TrySetResult(revalidated);
        }

        navigator.CommandSettled += Handler;
        return done.Task.WaitAsync(bound);
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");
}
