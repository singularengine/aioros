using System.Net;
using Aioros.Pages;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class StaticFileTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _root = Directory.CreateTempSubdirectory("aioros-webroot-").FullName;

    [Fact]
    [Trait("AC", "SSR-165")]
    public async Task StaticFilesWinOverParameterizedPagesAsync()
    {
        Directory.CreateDirectory(Path.Combine(_root, "ports"));
        await File.WriteAllBytesAsync(Path.Combine(_root, "ports", "semi.png"), Png, TestContext.Current.CancellationToken);
        var site = new Site();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = _root });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options);
        await using var app = builder.Build();
        app.UseStaticFiles();
        app.MapAiorosPages([new PageDefinition("/ports/{app}", "Pages/ports/[app]/Page.axaml", _ => new Model("Port"), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var http = app.GetTestClient();

        using var file = await http.GetAsync(new Uri("/ports/semi.png", UriKind.Relative), TestContext.Current.CancellationToken);
        var bytes = await file.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var renders = site.Engine.Renders.Count;
        using var page = await http.GetAsync(new Uri("/ports/semi", UriKind.Relative), TestContext.Current.CancellationToken);
        using var dotted = await http.GetAsync(new Uri("/ports/v1.2", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, bytes);
        Assert.Equal(0, renders);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("<p>Port</p>", await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, dotted.StatusCode);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
