using System.Text;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class NavigatorTests
{
    [Fact]
    [Trait("AC", "SSR-32")]
    public async Task CommandsNavigateWithSeeOtherAsync()
    {
        var site = new Site();
        site.Engine.Command = Mover.Command("/inbox/7");
        var posted = Http(form: "_btn=1%7Cx");
        await site.Handler.PostAsync(posted, MoverPage());
        var known = new Site();
        known.Engine.Command = Mover.Command("/inbox/8");
        known.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: true));
        var script = Http(form: "_btn=1%7Cx");
        script.Request.Headers[PageHandler.PlanHeader] = FakeEngine.Page(reusable: true).Plan.Template.Fingerprint;
        await known.Handler.PostAsync(script, MoverPage());

        Assert.Equal((303, "/inbox/7", string.Empty), (posted.Response.StatusCode, posted.Response.Headers.Location.ToString(), Site.Body(posted)));
        Assert.Empty(site.Engine.Renders);
        Assert.Equal((303, "/inbox/8", string.Empty), (script.Response.StatusCode, script.Response.Headers.Location.ToString(), Site.Body(script)));
        Assert.Null(script.Response.ContentType);
    }

    [Fact]
    [Trait("AC", "SSR-34")]
    public async Task NavigationsWhileLoadingRedirectAsync()
    {
        var site = new Site();
        var created = Http();
        await site.Handler.GetAsync(created, MoverPage(created: "/login"));
        var loaded = Http();
        await site.Handler.GetAsync(loaded, new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model(), [new LayoutDefinition("Pages/Layout.axaml", services => new Mover(services.GetRequiredService<INavigator>(), loaded: "/welcome"))]));
        var posted = Http(form: "_btn=1%7Cx");
        await new Site().Handler.PostAsync(posted, MoverPage(loaded: "/signed-out"));

        Assert.Equal((302, "/login"), (created.Response.StatusCode, created.Response.Headers.Location.ToString()));
        Assert.Equal((302, "/welcome"), (loaded.Response.StatusCode, loaded.Response.Headers.Location.ToString()));
        Assert.Empty(site.Engine.Renders);
        Assert.Equal((303, "/signed-out"), (posted.Response.StatusCode, posted.Response.Headers.Location.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-35")]
    public async Task RelativePathsResolveAgainstThePageAsync()
    {
        var locations = new List<string>();
        foreach (var path in (string[])["edit", "../about", "?page=2"])
        {
            var site = new Site();
            site.Engine.Command = Mover.Command(path);
            var http = Http("/crm/list", "_btn=1%7Cx");
            await site.Handler.PostAsync(http, MoverPage());
            locations.Add(http.Response.Headers.Location.ToString());
        }

        Assert.Equal(["/crm/edit", "/about", "/crm/list?page=2"], locations);
    }

    [Fact]
    [Trait("AC", "SSR-36")]
    public async Task TheNavigatorWinsOverRedirectToAsync()
    {
        var page = new Site();
        page.Engine.Command = models =>
        {
            ((Mover)models[^1]!).RedirectTo = "/old";
            return ((Mover)models[^1]!).GoAsync("/new");
        };
        var fromPage = Http(form: "_btn=1%7Cx");
        await page.Handler.PostAsync(fromPage, MoverPage());
        var layout = Http();
        await new Site().Handler.GetAsync(layout, new PageDefinition("/inbox", "Pages/inbox/Page.axaml", services => new Mover(services.GetRequiredService<INavigator>(), created: "/new"), [new LayoutDefinition("Pages/Layout.axaml", _ => new Model { RedirectTo = "/login" })]));
        var several = new Site();
        several.Engine.Command = Mover.Command("/first", "/second");
        var last = Http(form: "_btn=1%7Cx");
        await several.Handler.PostAsync(last, MoverPage());
        var fallback = Http();
        await new Site().Handler.GetAsync(fallback, new PageDefinition("/inbox", "Pages/inbox/Page.axaml", services => new Mover(services.GetRequiredService<INavigator>()) { RedirectTo = "/legacy" }, []));

        Assert.Equal("/new", fromPage.Response.Headers.Location.ToString());
        Assert.Equal("/new", layout.Response.Headers.Location.ToString());
        Assert.Equal("/second", last.Response.Headers.Location.ToString());
        Assert.Equal((302, "/legacy"), (fallback.Response.StatusCode, fallback.Response.Headers.Location.ToString()));
    }

    [Fact]
    [Trait("AC", "SSR-37")]
    public async Task OtherSitesAreAllowedAndOtherSchemesRefusedAsync()
    {
        var site = new Site();
        site.Engine.Command = Mover.Command("https://pay.example.com/checkout?id=1");
        var external = Http(form: "_btn=1%7Cx");
        await site.Handler.PostAsync(external, MoverPage());
        var refused = new Site();
        refused.Engine.Command = Mover.Command("javascript:alert(1)");
        var script = Http(form: "_btn=1%7Cx");
        var navigator = new PageNavigator();

        await Assert.ThrowsAsync<ArgumentException>(() => refused.Handler.PostAsync(script, MoverPage()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await navigator.NavigateAsync(new NavigationRequest("data:text/html,hi"), CancellationToken.None));
        Assert.Equal((303, "https://pay.example.com/checkout?id=1"), (external.Response.StatusCode, external.Response.Headers.Location.ToString()));
        Assert.Null(script.Response.Headers.Location.FirstOrDefault());
        Assert.Null(navigator.Requested);
    }

    [Fact]
    [Trait("AC", "SSR-66")]
    public async Task TheAppsOwnNavigationInterfaceRedirectsAsync()
    {
        var site = new Site();
        site.Engine.Command = models => new ValueTask(((Inbox)models[^1]!).SendAsync("/inbox/9"));
        var posted = Http(form: "_btn=1%7Cx");
        await site.Handler.PostAsync(posted, InboxPage());
        var created = Http();
        await new Site().Handler.GetAsync(created, InboxPage(created: "/login"));
        var both = new Site();
        both.Engine.Command = async models =>
        {
            await ((Inbox)models[^1]!).SendAsync("/first");
            await ((Mover)models[0]!).GoAsync("/second");
        };
        var last = Http(form: "_btn=1%7Cx");
        await both.Handler.PostAsync(last, InboxPage());

        Assert.Equal((303, "/inbox/9", string.Empty), (posted.Response.StatusCode, posted.Response.Headers.Location.ToString(), Site.Body(posted)));
        Assert.Empty(site.Engine.Renders);
        Assert.Equal((302, "/login"), (created.Response.StatusCode, created.Response.Headers.Location.ToString()));
        Assert.Equal((303, "/second"), (last.Response.StatusCode, last.Response.Headers.Location.ToString()));
    }

    private static PageDefinition InboxPage(string? created = null) =>
        new("/inbox", "Pages/inbox/Page.axaml", services => new Inbox(services.GetRequiredService<IInboxNavigator>(), created), [
            new LayoutDefinition("Pages/Layout.axaml", services => new Mover(services.GetRequiredService<INavigator>())),
        ]);

    private static PageDefinition MoverPage(string? created = null, string? loaded = null) =>
        new("/inbox", "Pages/inbox/Page.axaml", services => new Mover(services.GetRequiredService<INavigator>(), created: created, loaded: loaded), []);

    private static DefaultHttpContext Http(string path = "/inbox", string? form = null)
    {
        var http = Site.Http();
        http.RequestServices = new ServiceCollection()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(services => services.GetRequiredService<PageNavigator>())
            .AddNavigation<IInboxNavigator>(Aioros.Generated.AiorosNavigators.All)
            .BuildServiceProvider()
            .CreateScope()
            .ServiceProvider;
        http.Request.Path = path;
        if (form is not null)
        {
            http.Request.Method = "POST";
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        }

        return http;
    }
}

public interface IInboxNavigator
{
    Task GoToAsync(string path);
}

internal sealed class Inbox
{
    private readonly IInboxNavigator _navigator;

    public Inbox(IInboxNavigator navigator, string? created)
    {
        _navigator = navigator;
        if (created is not null)
        {
            _ = navigator.GoToAsync(created);
        }
    }

    public Task SendAsync(string path) => _navigator.GoToAsync(path);
}
