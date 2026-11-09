using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ChoiceScriptTests
{
    private const string Options = """
        <button type="submit" name="_btn" value="{0}0|" role="option" data-aioros-chooses="0" aria-checked="false" id="{0}0">Next.js</button>
        <button type="submit" name="_btn" value="{0}1|" role="option" data-aioros-chooses="1" aria-checked="false" id="{0}1">SvelteKit</button>
        <button type="submit" name="_btn" value="{0}2|" role="option" data-aioros-chooses="2" aria-checked="false" id="{0}2">Remix</button>
        """;

    private const string State = "p => { const v = document.querySelector('#' + p + ' [data-aioros-shows]'), h = document.querySelector('#' + p + ' > input[type=hidden]'); return [v.textContent, h.value, h.disabled, document.getElementById(p + 'm').matches(':popover-open'), [...document.querySelectorAll('#' + p + ' [data-aioros-chooses]')].sort((x, y) => x.dataset.aiorosChooses - y.dataset.aiorosChooses).map(o => o.getAttribute('aria-checked')[0]).join('')].join() }";

    [Fact]
    [Trait("AC", "SSR-234")]
    public async Task ListsChooseInTheBrowserAsync()
    {
        var steps = new Steps();
        await using var app = await HostAsync(Choosing(steps), "/choice", steps);
        var plain = new Site();
        plain.Engine.Plan = new RenderPlan([], [new LiteralNode("<button id=\"p\">Plain</button>")], []);
        await using var other = await HostAsync(plain, "/plain", new Steps());
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var modules = new[] { ChoiceScript.Prefix, FilterScript.Prefix }.Select(prefix => page.WaitForResponseAsync(response => response.Url.Contains(prefix, StringComparison.Ordinal))).ToList();
        var plan = page.WaitForResponseAsync(response => response.Url.Contains("/_aioros/plan/", StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/choice");
        await Task.WhenAll(modules).WaitAsync(TestContext.Current.CancellationToken);
        await plan.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", ChoiceScript.Path);
        await page.EvaluateAsync("u => import(u).then(() => 0)", FilterScript.Path);
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" || request.ResourceType == "document" ? 1 : 0;

        await page.ClickAsync("#at");
        await page.ClickAsync("#a1");
        var chosen = await page.EvaluateAsync<string>(State, "a");
        await page.ClickAsync("#at");
        await page.ClickAsync("#a1");
        var none = await page.EvaluateAsync<string>(State, "a");
        await page.ClickAsync("#at");
        await page.FillAsync("#aq", "rem");
        await page.PressAsync("#aq", "Enter");
        var entered = await page.EvaluateAsync<string>(State, "a");
        var sent = requests;
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#next");
        await answered;
        await page.WaitForFunctionAsync("() => document.getElementById('step').dataset.n === '1'", null, new() { PollingInterval = 20 });
        await page.ClickAsync("#at");
        await page.ClickAsync("#a1");
        var renewed = await page.EvaluateAsync<string>(State, "a");
        var posts = new List<string>();
        page.Request += (_, request) => posts.Add(request.Method == "POST" ? Uri.UnescapeDataString(request.PostData ?? string.Empty) : string.Empty);
        var changed = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#bt");
        await page.ClickAsync("#b2");
        await changed;
        var loads = new List<string>();
        var second = await browser.NewPageAsync();
        second.Request += (_, request) => loads.Add(request.Url);
        await second.GotoAsync(Address(other) + "/plain", new() { WaitUntil = WaitUntilState.NetworkIdle });

        Assert.Equal("SvelteKit #0,1,false,false,ftf", chosen);
        Assert.Equal("Select...,-1,false,false,fff", none);
        Assert.Equal("Remix,2,false,false,fft", entered);
        Assert.Equal(0, sent);
        Assert.Equal("SvelteKit #1,1,false,false,ftf", renewed);
        Assert.Contains(posts, post => post.Contains("_chg=1", StringComparison.Ordinal) && post.Contains("f1=2", StringComparison.Ordinal));
        Assert.DoesNotContain(loads, url => url.Contains(ChoiceScript.Prefix, StringComparison.Ordinal));
    }

    private static string List(string id, string field) => $"""
        <div id="{id}" data-aioros-choice data-aioros-toggle>
          <button type="button" id="{id}t" popovertarget="{id}m"><div data-aioros-shows>Select...</div></button>
          <div popover id="{id}m"><input type="text" id="{id}q" data-aioros-filter role="combobox" aria-autocomplete="list" aria-expanded="true" name="f{(id == "a" ? 8 : 9)}">
            <div role="listbox">{Options.Replace("{0}", id, StringComparison.Ordinal)}</div>
          </div>
          {field}
          <template data-aioros-value="-1"><div data-aioros-shows>Select...</div></template>
          <template data-aioros-value="0"><div data-aioros-shows>Next.js</div></template>
          <template data-aioros-value="1"><div data-aioros-shows>SvelteKit #
        """;

    private static string Tail => """
        </div></template>
          <template data-aioros-value="2"><div data-aioros-shows>Remix</div></template>
        </div>
        """;

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static Site Choosing(Steps steps)
    {
        var site = new Site();
        site.Engine.Command = _ =>
        {
            steps.Index++;
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<button id=\"next\">Next</button><i id=\"step\" data-n=\""), new ValueNode(static model => ((Steps)model!).Index),
            new LiteralNode("\"></i>" + List("a", "<input type=\"hidden\" name=\"f0\" value=\"-1\">")), new ValueNode(static model => ((Steps)model!).Index),
            new LiteralNode(Tail + List("b", "<input type=\"hidden\" name=\"f1\" value=\"-1\" disabled data-change>")), new ValueNode(static model => ((Steps)model!).Index),
            new LiteralNode(Tail),
        ], []);
        return site;
    }

    private static async Task<WebApplication> HostAsync(Site site, string path, Steps steps)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition(path, $"Pages{path}/Page.axaml", _ => steps, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Steps : IPageModel
    {
        public string Title => "Choice";

        public int Index { get; set; }
    }
}
