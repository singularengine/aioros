using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Ssr.Data;
using Aioros.Tests.SignIn;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopSignInTests(DesktopFixture fixture)
{
    private const string Contract = "c1";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SIGNIN-08")]
    public async Task ARemainingUnauthorizedSignsInThroughTheBrowserAsync()
    {
        await using var server = await AuthorizationServer.StartAsync(map: static app => app.MapPageData([Mine]));
        var cookie = await server.CookieAsync();
        var store = new SessionTokenStore();
        var signIn = new SignInClient(server.Options, server.Client(), store, TimeProvider.System);
        await using var services = new ServiceCollection()
            .AddSingleton(signIn)
            .AddSingleton<ILoaderResults>(new RemoteLoaderResults(server.Client(new SignedInHandler(signIn))))
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider();
        var browser = server.Browser(cookie);

        var outcome = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [Mine]);
            var failures = new List<Exception>();
            navigator.Failed += (_, exception) => failures.Add(exception);
            var opened = new List<(string Path, List<string> Texts, bool Stored)>();
            navigator.Browser = async address =>
            {
                opened.Add((address.AbsolutePath, [.. DesktopFixture.Texts(window)], await store.ReadAsync(TestContext.Current.CancellationToken) is not null));
                await browser(address);
            };

            await navigator.TryNavigateAsync("/mine");
            var signedIn = (navigator.Current, Texts: DesktopFixture.Texts(window).ToList());
            await navigator.TryNavigateAsync("/login?ReturnUrl=%2Fmine");
            var again = navigator.Current;
            navigator.Browser = _ =>
            {
                DesktopFixture.Settle(window);
                window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return Task.CompletedTask;
            };
            await navigator.TryNavigateAsync("/login");
            var cancelled = DesktopFixture.Texts(window).ToList();
            window.Close();
            return (opened, signedIn, again, failures, cancelled);
        });

        Assert.Equal(2, outcome.opened.Count);
        Assert.All(outcome.opened, open => Assert.Equal("/connect/authorize", open.Path));
        Assert.Equal(["Continue in your browser", "A browser window opened to sign you in. This window continues once you are signed in.", "Cancel"], outcome.opened[0].Texts);
        Assert.All(outcome.opened, open => Assert.False(open.Stored));
        Assert.Equal(("/mine", "mine of ana"), (outcome.signedIn.Current, outcome.signedIn.Texts.Single()));
        Assert.Equal("/mine", outcome.again);
        Assert.IsAssignableFrom<OperationCanceledException>(Assert.Single(outcome.failures));
        Assert.Equal(["Shell", "Something went wrong"], outcome.cancelled);
        Assert.Null(await store.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SIGNIN-17")]
    public async Task TheSignOutRouteRevokesTheTokensAndOffersToSignInAgainAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        await using var server = await AuthorizationServer.StartAsync(map: static app => app.MapPageData([Mine]));
        var store = new SessionTokenStore();
        var signIn = new SignInClient(server.Options, server.Client(), store, TimeProvider.System);
        await using var services = new ServiceCollection()
            .AddSingleton(signIn)
            .AddSingleton<ILoaderResults>(new RemoteLoaderResults(server.Client(new SignedInHandler(signIn))))
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider();
        var browser = server.Browser(await server.CookieAsync());

        var outcome = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [DesktopFixture.Pages[0], Mine]);
            navigator.Browser = browser;
            await navigator.TryNavigateAsync("/mine");
            var held = await store.ReadAsync(bound);
            await navigator.TryNavigateAsync("/logout");
            var signedOut = (Texts: DesktopFixture.Texts(window).ToList(), Stored: await store.ReadAsync(bound));
            var arrived = DesktopFixture.NextNavigationAsync(navigator, bound);
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Sign in")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var home = await arrived;
            var signedIn = await store.ReadAsync(bound);
            window.Close();
            return (held, signedOut, home, signedIn);
        });

        using var client = server.Client();
        using var form = new FormUrlEncodedContent([KeyValuePair.Create("grant_type", "refresh_token"), KeyValuePair.Create("refresh_token", outcome.held!.RefreshToken), KeyValuePair.Create("client_id", server.Options.ClientId)]);
        using var refreshed = await client.PostAsync(new Uri("/connect/token", UriKind.Relative), form, bound);

        Assert.Equal(["You are signed out", "Sign in"], outcome.signedOut.Texts);
        Assert.Null(outcome.signedOut.Stored);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refreshed.StatusCode);
        Assert.Equal("/", outcome.home);
        Assert.NotNull(outcome.signedIn);
    }

    private static readonly PageDefinition Mine = new("/mine", "Pages/items/[id]/Page.axaml", static _ => throw new InvalidOperationException("A view model with a loader is created from its data."), [])
    {
        Access = [new PageAccess(null, null)],
        DataContract = Contract,
        Loader = new LoaderDefinition(
            static (_, request, _) => ValueTask.FromResult(new LoaderOutcome(new ItemData("mine of " + request.User.Identity?.Name))),
            static (_, data) => new Item((ItemData)data!))
        {
            Write = static (writer, data) => writer.WriteStringValue(((ItemData)data!).Label),
            Read = static element => new ItemData(element.GetString()!),
        },
    };
}
