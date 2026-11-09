using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class UploadScriptTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-142")]
    public async Task ChosenFilesArePostedAsMultipartAsync()
    {
        var box = new Box();
        var site = new Site();
        var upload = new RenderPlan([], [
            new LiteralNode("<p id=\"got\">"), new ValueNode(static model => ((Box)model!).Received), new LiteralNode("</p>"),
            new LiteralNode("<label id=\"pick\">Choose<input type=\"file\" name=\"f0\"></label><button id=\"send\" type=\"submit\" name=\"_btn\" value=\"1|\">Send</button>"),
        ], []);
        var plain = new RenderPlan([], [new LiteralNode("<input name=\"f0\" value=\"Ada\"><button id=\"send\" type=\"submit\" name=\"_btn\" value=\"1|\">Send</button>")], []);
        site.Engine.PlanFor = page => page.Route == "/upload" ? upload : plain;
        site.Engine.Plan = upload;
        site.Engine.Command = _ =>
        {
            box.Received = string.Join(",", site.Engine.Uploaded) + "|" + site.Engine.Posted!["_btn"];
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, box);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var types = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                types.Add(request.Headers.GetValueOrDefault("content-type") ?? string.Empty);
            }
        };

        await page.GotoAsync(Address(app) + "/upload");
        await page.SetInputFilesAsync("input[name=f0]", new FilePayload { Name = "notes.txt", MimeType = "text/plain", Buffer = "hello"u8.ToArray() });
        var chosen = await page.GetAttributeAsync("#pick", "data-chosen");
        await page.ClickAsync("#send");
        await page.WaitForFunctionAsync("() => document.querySelector('#got').textContent !== ''").WaitAsync(TestContext.Current.CancellationToken);
        var shown = await page.TextContentAsync("#got");
        site.Engine.Plan = plain;
        await page.GotoAsync(Address(app) + "/plain");
        await page.RunAndWaitForResponseAsync(() => page.ClickAsync("#send"), response => response.Request.Method == "POST");

        Assert.Equal("notes.txt", chosen);
        Assert.Equal("hello|1|", shown);
        Assert.StartsWith("multipart/form-data", types[0], StringComparison.Ordinal);
        Assert.StartsWith("application/x-www-form-urlencoded", types[1], StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-251")]
    public async Task AChosenFileNameIsWrittenInItsNameTextAsync()
    {
        var box = new Box();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<style>label[data-chosen]:not(:has([data-aioros-file-name]))::after{content:attr(data-chosen)}</style>"),
            new LiteralNode("<label for=\"picture\">Picture</label><label id=\"themed\"><div>Choose File</div><div data-aioros-file-name>No file chosen</div><input type=\"file\" id=\"picture\" name=\"f0\"></label>"),
            new LiteralNode("<label id=\"plain\">Documents<input type=\"file\" name=\"f1\" multiple></label>"),
        ], []);
        await using var app = await StartAsync(site, box);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var posts = 0;
        page.Request += (_, request) => posts += request.Method == "POST" ? 1 : 0;

        await page.GotoAsync(Address(app) + "/upload").WaitAsync(TestContext.Current.CancellationToken);
        await page.SetInputFilesAsync("input[name=f0]", new FilePayload { Name = "photo.png", MimeType = "image/png", Buffer = [1, 2, 3] });
        await page.SetInputFilesAsync("input[name=f1]", [new FilePayload { Name = "a.pdf", MimeType = "application/pdf", Buffer = [1] }, new FilePayload { Name = "b.pdf", MimeType = "application/pdf", Buffer = [2] }]);
        var shown = await page.EvaluateAsync<string[]>("() => [document.querySelector('[data-aioros-file-name]').textContent, getComputedStyle(document.getElementById('themed'), '::after').content, getComputedStyle(document.getElementById('plain'), '::after').content]");

        Assert.Equal(["photo.png", "none", "\"a.pdf, b.pdf\""], shown);
        Assert.Equal(0, posts);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> StartAsync(Site site, Box box)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/upload", "Pages/upload/Page.axaml", _ => box, []), new PageDefinition("/plain", "Pages/plain/Page.axaml", _ => box, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Box : IPageModel
    {
        public string Title => "Upload";

        public string Received { get; set; } = string.Empty;
    }
}
