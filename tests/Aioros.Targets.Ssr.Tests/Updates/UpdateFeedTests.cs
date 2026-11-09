using System.Net;
using System.Net.Http.Headers;
using Aioros.Targets.Ssr.Updates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Aioros.Targets.Ssr.Tests.Updates;

public sealed class UpdateFeedTests : IDisposable
{
    private const string NoCache = "no-cache";
    private const string Immutable = "public, max-age=31536000, immutable";

    private readonly string _folder = Directory.CreateTempSubdirectory("aioros-feed-").FullName;

    [Fact]
    [Trait("AC", "INSTALL-18")]
    public async Task TheSiteServesTheFeedWithItsTypesAndCachingAsync()
    {
        foreach (var name in new[] { "releases.linux.json", "assets.linux.json", "RELEASES", "Shop-1.0.1-linux-full.nupkg", "Shop-1.0.1-linux-delta.nupkg", "Shop.AppImage", "Shop-win-Setup.exe", "Shop-win-Portable.zip", "notes.txt" })
        {
            await File.WriteAllTextAsync(Path.Combine(_folder, name), "0123456789" + name, TestContext.Current.CancellationToken);
        }

        await using var site = await StartAsync(_folder);
        await using var bare = await StartAsync(null);
        using var http = site.GetTestClient();

        var answers = new List<(string Name, HttpStatusCode Status, string? Type, string? Cache)>();
        foreach (var name in new[] { "releases.linux.json", "assets.linux.json", "RELEASES", "Shop-1.0.1-linux-full.nupkg", "Shop-1.0.1-linux-delta.nupkg", "Shop.AppImage", "Shop-win-Setup.exe", "Shop-win-Portable.zip" })
        {
            using var response = await http.GetAsync(new Uri("/updates/" + name, UriKind.Relative), TestContext.Current.CancellationToken);
            answers.Add((name, response.StatusCode, response.Content.Headers.ContentType?.MediaType, response.Headers.CacheControl?.ToString()));
        }

        using var range = new HttpRequestMessage(HttpMethod.Get, new Uri("/updates/Shop-1.0.1-linux-full.nupkg", UriKind.Relative)) { Headers = { Range = new RangeHeaderValue(2, 5) } };
        using var partial = await http.SendAsync(range, TestContext.Current.CancellationToken);
        var refused = new List<HttpStatusCode>();
        foreach (var path in new[] { "/updates/notes.txt", "/updates/", "/updates/missing.nupkg" })
        {
            using var response = await http.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            refused.Add(response.StatusCode);
        }

        using var unconfigured = await bare.GetTestClient().GetAsync(new Uri("/updates/releases.linux.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("releases.linux.json", HttpStatusCode.OK, "application/json", NoCache),
                ("assets.linux.json", HttpStatusCode.OK, "application/json", NoCache),
                ("RELEASES", HttpStatusCode.OK, "text/plain", NoCache),
                ("Shop-1.0.1-linux-full.nupkg", HttpStatusCode.OK, "application/octet-stream", Immutable),
                ("Shop-1.0.1-linux-delta.nupkg", HttpStatusCode.OK, "application/octet-stream", Immutable),
                ("Shop.AppImage", HttpStatusCode.OK, "application/vnd.appimage", NoCache),
                ("Shop-win-Setup.exe", HttpStatusCode.OK, "application/vnd.microsoft.portable-executable", NoCache),
                ("Shop-win-Portable.zip", HttpStatusCode.OK, "application/zip", NoCache),
            ],
            answers);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal("2345", await partial.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound], refused);
        Assert.Equal(HttpStatusCode.NotFound, unconfigured.StatusCode);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static async Task<WebApplication> StartAsync(string? folder)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (folder is not null)
        {
            builder.Configuration[UpdateFeed.FolderSetting] = folder;
        }

        var app = builder.Build();
        app.UseUpdateFeed(app.Configuration[UpdateFeed.FolderSetting]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
