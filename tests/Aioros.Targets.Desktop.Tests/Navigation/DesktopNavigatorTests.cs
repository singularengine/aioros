using Microsoft.Extensions.DependencyInjection;
using Aioros.Targets.Desktop.Navigation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopNavigatorTests(DesktopFixture fixture)
{
    [Fact]
    [Trait("AC", "DESKTOP-01")]
    public async Task PathsShowTheirPageInsideTheLayoutAsync()
    {
        var (texts, title) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/items/42?tab=open%20notes");
            var result = (DesktopFixture.Texts(window).ToList(), window.Title);
            window.Close();
            return result;
        });

        Assert.Equal(["Shell", "42/open notes"], texts);
        Assert.Equal("Item 42/open notes", title);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-02")]
    public async Task LinksAndHyperlinksNavigateAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var (afterLink, afterHyperlink) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/");
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "Open item");
            var first = await navigated;
            await navigator.NavigateAsync("/");
            navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "About link");
            var second = await navigated;
            window.Close();
            return (first, second);
        });

        Assert.Equal("/items/7", afterLink);
        Assert.Equal("/about", afterHyperlink);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-97")]
    public async Task LinkedRunsNavigateAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var (overLink, overText, stayed, path) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/inline");
            DesktopFixture.Settle(window);
            var sentence = window.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "Sentence");
            Point At(double share) => sentence.TranslatePoint(new Point(sentence.Bounds.Width * share, sentence.Bounds.Height / 2), window) ?? default;
            window.MouseMove(At(0.85));
            var link = sentence.Cursor;
            window.MouseMove(At(0.15));
            var text = sentence.Cursor;
            window.MouseDown(At(0.15), MouseButton.Left);
            window.MouseUp(At(0.15), MouseButton.Left);
            DesktopFixture.Settle(window);
            var current = navigator.Current;
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            window.MouseDown(At(0.85), MouseButton.Left);
            window.MouseUp(At(0.85), MouseButton.Left);
            var next = await navigated;
            window.Close();
            return (link, text, current, next);
        });

        Assert.NotNull(overLink);
        Assert.Null(overText);
        Assert.Equal("/inline", stayed);
        Assert.Equal("/items/7", path);
    }

    [Fact]
    [Trait("AC", "DESKTOP-03")]
    public async Task BackReturnsToThePreviousPageAsync()
    {
        var (texts, loads) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            Item.Loads = 0;
            await navigator.NavigateAsync("/items/3");
            await navigator.NavigateAsync("/about");
            await navigator.BackAsync();
            var result = (DesktopFixture.Texts(window).ToList(), Item.Loads);
            window.Close();
            return result;
        });

        Assert.Equal(["Shell", "3"], texts);
        Assert.Equal(2, loads);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-04")]
    public async Task CommandsRedirectAfterRunningAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var path = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/form");
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "Save");
            var result = await navigated;
            window.Close();
            return result;
        });

        Assert.Equal("/about", path);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-16")]
    public async Task AsynchronousCommandsAreAwaitedBeforeTheirRedirectAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var (path, sends, failure, shown) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/later");
            var model = Later.Last!;
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "Send");
            model.Gate.SetResult();
            var redirected = await navigated;
            await navigator.NavigateAsync("/later");
            var failed = NextFailureAsync(navigator, bound);
            DesktopFixture.Click(window, "Break");
            var exception = await failed;
            var texts = DesktopFixture.Texts(window).ToList();
            window.Close();
            return (redirected, model.Sends, exception, texts);
        });

        Assert.Equal("/about", path);
        Assert.Equal(1, sends);
        Assert.Equal("The save failed.", failure.Message);
        Assert.Contains("Something went wrong", shown);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-36")]
    public async Task MenuItemCommandsAreAwaitedBeforeTheirRedirectAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var (paths, sends, failure, shown, rebound) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            var paths = new List<string>();
            var models = new List<Later>();
            await navigator.NavigateAsync("/menu");
            models.Add(Later.Last!);
            paths.Add(await SendAsync(navigator, models[^1], () => DesktopFixture.Click(window, "Send")));
            await navigator.NavigateAsync("/menu");
            models.Add(Later.Last!);
            var area = window.GetLogicalDescendants().OfType<Border>().First(border => border.Name == "Area");
            area.ContextMenu!.Open(area);
            DesktopFixture.Settle(window);
            paths.Add(await SendAsync(navigator, models[^1], () => Raise(area.ContextMenu.Items.OfType<MenuItem>().Single())));
            await navigator.NavigateAsync("/menu");
            var page = window.GetLogicalDescendants().OfType<UserControl>().Last();
            models.Add(new Later());
            page.DataContext = models[^1];
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigator.CommandSettled += (_, revalidated) => settled.TrySetResult(revalidated);
            Raise(Item(window, "Send"));
            models[^1].Gate.SetResult();
            var rebound = await settled.Task.WaitAsync(bound);
            await navigator.NavigateAsync("/menu");
            var failed = NextFailureAsync(navigator, bound);
            Raise(Item(window, "Break"));
            var exception = await failed;
            var texts = DesktopFixture.Texts(window).ToList();
            window.Close();
            return (paths, models.Select(static model => model.Sends).ToList(), exception, texts, rebound);
        });

        Assert.Equal(["/about", "/about"], paths);
        Assert.False(rebound);
        Assert.Equal([1, 1, 1], sends);
        Assert.Equal("The save failed.", failure.Message);
        Assert.Contains("Something went wrong", shown);

        async Task<string> SendAsync(DesktopNavigator navigator, Later model, Action click)
        {
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            click();
            model.Gate.SetResult();
            return await navigated;
        }

        static MenuItem Item(Window window, string header) => window.GetLogicalDescendants().OfType<MenuItem>().First(item => Equals(item.Header, header));

        static void Raise(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    [Fact]
    [Trait("AC", "DESKTOP-05")]
    public async Task ThemePathsSwitchTheThemeInPlaceAsync()
    {
        var (dark, current) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/about");
            await navigator.NavigateAsync("/theme/dark");
            var result = (global::Avalonia.Application.Current!.ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Dark, navigator.Current);
            await navigator.NavigateAsync("/theme/light");
            window.Close();
            return result;
        });

        Assert.True(dark);
        Assert.Equal("/about", current);
    }

    [Fact]
    [Trait("AC", "DESKTOP-93")]
    public async Task TheSystemThemePathFollowsTheOperatingSystemAsync()
    {
        var (requested, current) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/about");
            await navigator.NavigateAsync("/theme/system");
            var result = (global::Avalonia.Application.Current!.RequestedThemeVariant, navigator.Current);
            await navigator.NavigateAsync("/theme/light");
            window.Close();
            return result;
        });

        Assert.Equal(global::Avalonia.Styling.ThemeVariant.Default, requested);
        Assert.Equal("/about", current);
    }

    [Fact]
    [Trait("AC", "DESKTOP-06")]
    public async Task UnknownPathsShowTheNotFoundPageAsync()
    {
        var (unknown, gone, bare) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/nowhere");
            var first = DesktopFixture.Texts(window).ToList();
            await navigator.NavigateAsync("/gone");
            var second = DesktopFixture.Texts(window).ToList();
            window.Close();
            var (plain, plainNavigator) = DesktopFixture.Open(withNotFound: false);
            await plainNavigator.NavigateAsync("/nowhere");
            var third = plain.Content;
            plain.Close();
            return (first, second, third);
        });

        Assert.Equal(["Shell", "Nothing here"], unknown);
        Assert.Equal(["Shell", "Nothing here"], gone);
        Assert.Null(bare);
    }

    [Fact]
    [Trait("AC", "DESKTOP-07")]
    public async Task LoadersRedirectAsync()
    {
        var (path, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/old");
            var result = (navigator.Current, DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.Equal("/about", path);
        Assert.Equal(["Shell", "About page"], texts);
    }

    [Fact]
    [Trait("AC", "DESKTOP-08")]
    public async Task LinksToThePageShownAreCurrentAsync()
    {
        var (items, about) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/items/42");
            var first = Current(window);
            await navigator.NavigateAsync("/about");
            var second = Current(window);
            window.Close();
            return (first, second);
        });

        Assert.Equal((true, false), items);
        Assert.Equal((false, true), about);
    }

    private static (bool Items, bool About) Current(global::Avalonia.Controls.Window window)
    {
        var borders = global::Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(window).OfType<global::Avalonia.Controls.Border>().ToList();
        return (borders.Single(border => border.Name == "ItemsTab").Classes.Contains(":current"), borders.Single(border => border.Name == "AboutTab").Classes.Contains(":current"));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-09")]
    public async Task FailedPagesShowTheErrorPageAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var (withError, failure) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/");
            var failed = NextFailureAsync(navigator, bound);
            DesktopFixture.Click(window, "Open broken");
            var exception = await failed;
            var shown = DesktopFixture.Texts(window).ToList();
            window.Close();
            return (shown, exception);
        });

        Assert.Contains("Something went wrong", withError);
        Assert.Contains("Shell", withError);
        Assert.Equal("The board is down.", failure.Message);
    }

    [Fact]
    [Trait("AC", "DESKTOP-10")]
    public async Task EachPageHasItsOwnScopeAsync()
    {
        using var services = new ServiceCollection().AddScoped<Lease>().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var (first, second, firstDisposed, secondDisposed) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services);
            var leases = DesktopFixture.Leases;
            leases.Clear();
            await navigator.NavigateAsync("/leased");
            await navigator.NavigateAsync("/leased");
            var result = (leases[0], leases[1], leases[0].Disposed, leases[1].Disposed);
            window.Close();
            return result;
        });

        Assert.NotSame(first, second);
        Assert.True(firstDisposed);
        Assert.False(secondDisposed);
    }

    [Fact]
    [Trait("AC", "DESKTOP-12")]
    public async Task SharedLayoutsStayMountedAsync()
    {
        var (sameShell, sameSection, inSection, index, shellAfter, outside) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/sections/pick");
            var shell = window.Content;
            var section = Layouts(window)[1];
            await navigator.NavigateAsync("/sections/one");
            var layouts = Layouts(window);
            var shown = DesktopFixture.Texts(window).ToList();
            var picked = Pick.Last!.Index;
            await navigator.NavigateAsync("/about");
            var result = (ReferenceEquals(shell, layouts[0]), ReferenceEquals(section, layouts[1]), shown, picked, ReferenceEquals(shell, window.Content), DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.True(sameShell);
        Assert.True(sameSection);
        Assert.Equal(["Shell", "/sections/one", "One"], inSection);
        Assert.Equal(2, index);
        Assert.True(shellAfter);
        Assert.Equal(["Shell", "About page"], outside);
    }

    private static List<global::Avalonia.Controls.UserControl> Layouts(global::Avalonia.Controls.Window window) =>
        [.. global::Avalonia.LogicalTree.LogicalExtensions.GetSelfAndLogicalDescendants((global::Avalonia.Controls.Control)window.Content!)
            .OfType<global::Avalonia.Controls.UserControl>()
            .Where(control => global::Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(control).OfType<Aioros.Avalonia.Controls.Outlet>().Any())];

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-13")]
    public async Task CommandsNavigateThroughTheInjectedNavigatorAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        using var services = DesktopFixture.Navigation();
        var (path, settled, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services);
            await navigator.NavigateAsync("/leave");
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "Go");
            var first = await navigated;
            var result = (first, navigator.Current, DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.Equal("/about", path);
        Assert.Equal("/about", settled);
        Assert.Equal(["Shell", "About page"], texts);
    }

    [Fact]
    [Trait("AC", "DESKTOP-14")]
    public async Task NavigationsWhileLoadingGoElsewhereAsync()
    {
        using var services = DesktopFixture.Navigation();
        var (entered, arrived, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services);
            await navigator.NavigateAsync("/entering");
            var first = navigator.Current;
            await navigator.NavigateAsync("/arriving");
            var result = (first, navigator.Current, DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.Equal("/about", entered);
        Assert.Equal("/items/5", arrived);
        Assert.Equal(["Shell", "5"], texts);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-15")]
    public async Task OtherSitesOpenOutsideTheWindowAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        using var services = DesktopFixture.Navigation();
        var (current, texts, refused) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services);
            await navigator.NavigateAsync("/leave");
            var model = Leaving.Last!;
            DesktopFixture.Click(window, "Away");
            await model.Away.Task.WaitAsync(bound);
            var stayed = (navigator.Current, DesktopFixture.Texts(window).ToList());
            var error = await Record.ExceptionAsync(async () => await model.Navigator.NavigateAsync(new Aioros.Navigation.NavigationRequest("javascript:alert(1)"), CancellationToken.None));
            window.Close();
            return (stayed.Current, stayed.Item2, error);
        });

        Assert.Equal("/leave", current);
        Assert.Equal(["Shell", "Go", "Away"], texts);
        Assert.IsType<ArgumentException>(refused);
    }

    /// <summary>The next <see cref="DesktopNavigator.Failed"/>, bounded by the test's own timeout so a regression fails instead of hanging.</summary>
    internal static Task<Exception> NextFailureAsync(DesktopNavigator navigator, CancellationToken bound)
    {
        var done = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        navigator.Failed += (_, exception) => done.TrySetResult(exception);
        return done.Task.WaitAsync(bound);
    }
}
