using System.Net.Http.Headers;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class UploadTests
{
    [Fact]
    [Trait("AC", "SSR-139")]
    public async Task PagesWithUploadsPostAsMultipartAsync()
    {
        var uploads = new Site();
        uploads.Engine.Uploads = true;
        var page = Site.Http();
        var plain = Site.Http();
        var planned = new CompiledPage(new RenderPlan([], [new LiteralNode("<p>")], []), string.Empty, "#fff", "#000", true) { Uploads = true };
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned), new PlannedPage("/plain", "light", planned with { Uploads = false })]).Json, new PlanSlotResolver());

        await uploads.Handler.GetAsync(page, Site.Page(() => new Model()));
        await new Site().Handler.GetAsync(plain, Site.Page(() => new Model()));

        Assert.Contains("<form method=\"post\" enctype=\"multipart/form-data\" novalidate class=\"c\"", Site.Body(page), StringComparison.Ordinal);
        Assert.Contains("<form method=\"post\" novalidate class=\"c\"", Site.Body(plain), StringComparison.Ordinal);
        Assert.Equal([true, false], saved.Select(entry => entry.Page.Uploads));
    }

    [Fact]
    [Trait("AC", "SSR-140")]
    public async Task MultipartPostsHandTheirFilesToTheEngineAsync()
    {
        var site = new Site();
        var http = Multipart(("f0", null, "Ada"), ("f1", "notes.txt", "hello"), ("f2", string.Empty, string.Empty));

        await site.Handler.PostAsync(http, Site.Page(() => new Model()));

        var file = Assert.Single(site.Engine.Files);
        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        Assert.Equal("Ada", site.Engine.Posted!["f0"]);
        Assert.Equal(("f1", "notes.txt", "text/plain", 5L), (file.Field, file.Name, file.ContentType, file.Length));
        Assert.Equal(["hello"], site.Engine.Uploaded);
        Assert.False(File.Exists(file.Path));
    }

    [Fact]
    [Trait("AC", "SSR-141")]
    public async Task FilesOverTheLimitAreRefusedAsync()
    {
        var site = new Site(new SsrOptions { UploadLimit = 4 });
        var over = Multipart(("f1", "notes.txt", "hello"));
        var at = Multipart(("f1", "four.txt", "four"));
        var forged = Multipart(("f1", "notes.txt", "hello"));
        forged.Request.Headers["Sec-Fetch-Site"] = "cross-site";

        await site.Handler.PostAsync(over, Site.Page(() => new Model()));
        var refused = site.Engine.Posted;
        await site.Handler.PostAsync(forged, Site.Page(() => new Model()));
        var forgedRead = forged.Request.Body.Position;
        await site.Handler.PostAsync(at, Site.Page(() => new Model()));

        Assert.Equal(10 * 1024 * 1024, new SsrOptions().UploadLimit);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, over.Response.StatusCode);
        Assert.Null(refused);
        Assert.Equal((StatusCodes.Status403Forbidden, 0L), (forged.Response.StatusCode, forgedRead));
        Assert.Equal(StatusCodes.Status200OK, at.Response.StatusCode);
        Assert.Equal(["four"], site.Engine.Uploaded);
    }

    private static DefaultHttpContext Multipart(params (string Name, string? File, string Content)[] parts)
    {
        using var content = new MultipartFormDataContent("aioros-boundary");
        foreach (var (name, file, text) in parts)
        {
            if (file is null)
            {
                content.Add(new StringContent(text), name);
                continue;
            }

            var part = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text));
            part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"" + name + "\"", FileName = "\"" + file + "\"" };
            content.Add(part);
        }

        var body = new MemoryStream();
        content.CopyTo(body, null, TestContext.Current.CancellationToken);
        body.Position = 0;
        var http = Site.Http();
        http.Request.Method = "POST";
        http.Request.ContentType = content.Headers.ContentType!.ToString();
        http.Request.Body = body;
        return http;
    }
}
