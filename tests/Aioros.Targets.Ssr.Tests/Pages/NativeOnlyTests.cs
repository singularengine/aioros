using System.Collections.Concurrent;
using System.Net;
using Aioros.Pages;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class NativeOnlyTests
{
    private const string Contract = "c1";

    [Fact]
    [Trait("AC", "NATIVE-09")]
    public async Task NativeOnlyRoutesAnswerWithTheBuiltInStandInAsync()
    {
        var loads = new ConcurrentQueue<string>();
        var site = new Site(new SsrOptions { Download = "https://example.test/download" });
        await using var app = await StartAsync(site, Files(loads));

        using var response = await app.GetTestClient().GetAsync(new Uri("/files", UriKind.Relative), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var post = await app.GetTestClient().PostAsync(new Uri("/files", UriKind.Relative), new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("<title>Files · Showcase</title>", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"Browse a folder &amp; its files.\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Files · Showcase</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Browse a folder &amp; its files.</p>", html, StringComparison.Ordinal);
        Assert.Contains("Available in the desktop app", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://example.test/download\" data-open-app>Open in the app</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://example.test/download\" data-aioros-download>Download</a>", html, StringComparison.Ordinal);
        Assert.Empty(loads);
        Assert.Empty(site.Engine.Renders);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    [Trait("AC", "NATIVE-10")]
    public async Task TheAppsStandInIsRenderedWithItsOwnModelsAsync()
    {
        var loads = new ConcurrentQueue<string>();
        var standIn = new PageDefinition(Aioros.Routing.Files.FileRoutes.NativeOnlyRoute, "Pages/NativeOnly.axaml", _ => null, [new LayoutDefinition("Pages/Layout.axaml", _ => new Model("Get the app"))]);
        var site = new Site(new SsrOptions { NativeOnly = standIn });
        await using var app = await StartAsync(site, Files(loads));

        using var response = await app.GetTestClient().GetAsync(new Uri("/files", UriKind.Relative), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.ETag);
        Assert.Contains("<title>Files</title>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Get the app</p>", html, StringComparison.Ordinal);
        Assert.Empty(loads);
        Assert.False(site.Cache.TryGet("/files", "light", out _));
    }

    [Fact]
    [Trait("AC", "NATIVE-11")]
    public async Task InstalledAppsGetTheRealPagesDataAsync()
    {
        var loads = new ConcurrentQueue<string>();
        await using var app = await StartAsync(new Site(), Files(loads));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/_data/files", UriKind.Relative));
        request.Headers.Add("X-Aioros-Contract", Contract);

        using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"data\":\"three folders\"", json, StringComparison.Ordinal);
        Assert.Equal(["loaded"], loads);
    }

    [Fact]
    [Trait("AC", "NATIVE-13")]
    public void NativeOnlyPagesAreNotPrecompiled()
    {
        var home = new PageDefinition("/", "Pages/Page.axaml", _ => null, []);
        var files = Files(new ConcurrentQueue<string>());
        var notFound = new PageDefinition("/_not-found", "Pages/NotFound.axaml", _ => null, []);
        var standIn = new PageDefinition(Aioros.Routing.Files.FileRoutes.NativeOnlyRoute, "Pages/NativeOnly.axaml", _ => null, []);

        var planned = Warmup.Planned([home, files], notFound, null, standIn);

        Assert.Equal(["/", "/_not-found", "/_native-only"], planned.Select(page => page.Route));
    }

    private static PageDefinition Files(ConcurrentQueue<string> loads) =>
        new("/files", "Pages/files/Page.axaml", _ => null, [new LayoutDefinition("Pages/Layout.axaml", _ => null) { Head = new PageHead("Showcase", "%s · Showcase") }])
        {
            Platforms = PagePlatforms.Desktop,
            Head = new PageHead("Files", Metadata: new PageMetadata("Browse a folder & its files.")),
            DataContract = Contract,
            Loader = new LoaderDefinition(
                (_, _, _) =>
                {
                    loads.Enqueue("loaded");
                    return ValueTask.FromResult(new LoaderOutcome("three folders"));
                },
                (_, data) => new Model((string)data!))
            {
                Write = static (writer, data) => writer.WriteStringValue((string?)data),
                Read = static element => element.GetString(),
            },
        };

    [Fact]
    [Trait("AC", "NATIVE-14")]
    public async Task TheStandInLoadsItsOpenInAppScriptFromAFileAsync()
    {
        var site = new Site(new SsrOptions { AppScheme = "showcase" });
        await using var app = await StartAsync(site, Files(new ConcurrentQueue<string>()));

        var html = await app.GetTestClient().GetStringAsync(new Uri("/files", UriKind.Relative), TestContext.Current.CancellationToken);
        var source = System.Text.RegularExpressions.Regex.Match(html, "<script src=\"(/_aioros/open/[0-9a-f]+\\.js)\" defer></script>").Groups[1].Value;
        using var script = await app.GetTestClient().GetAsync(new Uri(source, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Contains("data-open-app", await script.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    private static async Task<WebApplication> StartAsync(Site site, params PageDefinition[] pages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options);
        var app = builder.Build();
        app.MapAiorosPages(pages);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
