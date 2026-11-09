using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class FormSubmitTests
{
    private const string Button = "() => { const b = document.createElement('button'); b.name = '_btn'; b.value = '1|x'; b.id = 'go'; b.textContent = 'Go'; document.querySelector('form').append(b); }";

    [Fact]
    [Trait("AC", "SSR-25")]
    public async Task FormsArePostedWithoutReloadingThePageAsync()
    {
        var site = new Site();
        var visits = 0;
        await using var app = await StartAsync(site, [
            new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Visit " + Interlocked.Increment(ref visits)), []),
            new PageDefinition("/move", "Pages/move/Page.axaml", services => new Model("Moving")
            {
                RedirectTo = services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Method == "POST" ? "/inbox" : null,
            }, []),
        ]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });

        await page.GotoAsync(address + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; const tall = document.createElement('div'); tall.style.height = '3000px'; document.body.append(tall); scrollTo(0, 500); }");
        await page.EvaluateAsync(Button);
        await page.EvaluateAsync("() => document.querySelector('#go').click()");
        await page.WaitForFunctionAsync("() => document.body.textContent.includes('Visit 2')");
        var inPlace = (await page.EvaluateAsync<int?>("() => window.marker"), page.Url, await page.EvaluateAsync<double>("() => scrollY"));

        await page.GotoAsync(address + "/move");
        await page.EvaluateAsync("() => { window.marker = 2; }");
        await page.EvaluateAsync(Button);
        await page.ClickAsync("#go");
        await page.WaitForFunctionAsync("() => location.pathname === '/inbox' && document.body.textContent.includes('Visit')");
        var moved = await page.EvaluateAsync<int?>("() => window.marker");

        site.Engine.Outcome = new PostOutcome(PostStatus.BadRequest, null);
        await page.EvaluateAsync(Button);
        var native = page.WaitForResponseAsync(response => response.Request.IsNavigationRequest && response.Request.Method == "POST");
        await page.ClickAsync("#go");
        var fallback = (await native).Status;

        Assert.Equal((1, address + "/inbox", 500d), inPlace);
        Assert.Equal(2, moved);
        Assert.Equal(400, fallback);
    }

    [Fact]
    [Trait("AC", "SSR-298")]
    public async Task APressWhileAPostIsPendingIsIgnoredAsync()
    {
        var site = new Site();
        var gate = new TaskCompletionSource();
        var presses = 0;
        site.Engine.Command = async models =>
        {
            Interlocked.Increment(ref presses);
            await gate.Task;
            ((Model)models[0]!).Title = "Answered";
        };
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Start"), [])]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var posts = 0;
        var navigations = 0;
        page.Request += (_, request) =>
        {
            posts += request.Method == "POST" ? 1 : 0;
            navigations += request.IsNavigationRequest && request.Method == "POST" ? 1 : 0;
        };

        await page.GotoAsync(address + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await page.EvaluateAsync(Button);
        var pending = page.WaitForRequestAsync(static request => request.Method == "POST");
        await page.ClickAsync("#go");
        await pending;
        await page.ClickAsync("#go");
        await page.ClickAsync("#go");
        var marker = await page.EvaluateAsync<int?>("() => window.marker");
        gate.SetResult();
        await page.WaitForFunctionAsync("() => document.body.textContent.includes('Answered')");

        Assert.Equal(1, marker);
        Assert.Equal(1, posts);
        Assert.Equal(0, navigations);
        Assert.Equal(1, presses);
        Assert.Equal(address + "/inbox", page.Url);
    }

    [Fact]
    [Trait("AC", "SSR-27")]
    public async Task CommandsAnsweredInPlaceArriveAsDynamicsAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<h1>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</h1>"),
            new ConditionNode(scope => ((Model)scope!).Title.Length > 12, [new LiteralNode("<p class=\"long\">long title</p>")]),
            new LiteralNode("<ul>"), new RepeatNode(scope => ((Model)scope!).Title.Split(' '), [new LiteralNode("<li>"), new ValueNode(scope => scope), new LiteralNode("</li>")]), new LiteralNode("</ul>"),
        ], []);
        var next = "Alpha & beta";
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model(next), [])]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answers = new List<string>();
        page.Response += (_, response) => answers.Add(response.Request.Method + " " + response.Headers.GetValueOrDefault("content-type"));

        await page.GotoAsync(address + "/inbox");
        await page.WaitForFunctionAsync("() => performance.getEntriesByType('resource').some(entry => entry.name.includes('/_aioros/plan/'))");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        next = "Gamma delta epsilon";
        await page.EvaluateAsync(Button);
        await page.ClickAsync("#go");
        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'Gamma delta epsilon'");
        var applied = await page.InnerHTMLAsync(".aioros-body");
        var expected = await page.EvaluateAsync<string>("async () => new DOMParser().parseFromString(await (await fetch(location.href)).text(), 'text/html').querySelector('.aioros-body').innerHTML");

        Assert.Equal(1, await page.EvaluateAsync<int?>("() => window.marker"));
        Assert.Contains("POST application/json", answers);
        Assert.Equal(expected, applied);
        Assert.Contains("<li>epsilon</li>", applied, StringComparison.Ordinal);
        Assert.Contains("class=\"long\"", applied, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-28")]
    public async Task LiveUpdatesArriveAsDynamicsAsync()
    {
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<h1>"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</h1><ul>"),
            new RepeatNode(scope => ((Model)scope!).Title.Split(' '), [new LiteralNode("<li>"), new ValueNode(scope => scope), new LiteralNode("</li>")]), new LiteralNode("</ul>"),
        ], []);
        var models = System.Threading.Channels.Channel.CreateUnbounded<Model>();
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ =>
        {
            var model = new Model("First words");
            models.Writer.TryWrite(model);
            return model;
        }, [], Live: true)]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var streams = new List<string>();
        page.Request += (_, request) => streams.Add(request.Url);

        var opened = page.WaitForRequestAsync(request => request.Url.Contains("/live?plan=", StringComparison.Ordinal));
        await page.GotoAsync(address + "/inbox");
        await opened;
        await page.WaitForFunctionAsync("() => true");
        var created = new List<Model>();
        while (created.Count < 3)
        {
            created.Add(await models.Reader.ReadAsync(TestContext.Current.CancellationToken));
        }

        foreach (var model in created)
        {
            model.Title = "Second set of words";
        }

        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'Second set of words'");
        var applied = await page.InnerHTMLAsync(".aioros-body");

        Assert.Contains(streams, url => url.Contains("/live?plan=", StringComparison.Ordinal));
        Assert.Equal("<h1>Second set of words</h1><ul><li>Second</li><li>set</li><li>of</li><li>words</li></ul>", applied);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-118")]
    public async Task InvalidatedViewsArriveAsRegionsInPlaceAsync()
    {
        const string Host = "Pages/inbox/Page.axaml";
        const string Key = Host + ":0.1";
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<h1>"), new ValueNode(static model => ((ViewKeyTests.Card)model!).Title), new LiteralNode("</h1><input id=\"kept\">"),
            new ViewNode(1, Key, [new LiteralNode("<b id=\"value\">"), new ValueNode(static model => ((ViewKeyTests.Card)model!).Title), new LiteralNode("</b>")], []),
        ], []);
        var sales = "before";
        var page = new PageDefinition("/inbox", Host, static _ => null, [], Live: true)
        {
            Loader = new LoaderDefinition(static (_, _, _) => ValueTask.FromResult(new LoaderOutcome("Inbox")), static (_, data) => new ViewKeyTests.Card((string)data!)),
            Views =
            [
                new ViewInstance(Key, 0, new ViewDefinition("Views/Card.axaml", static _ => null)
                {
                    Loader = new LoaderDefinition(
                        (services, _, _) =>
                        {
                            services.GetRequiredService<Aioros.Functions.IDataKeys>().Read("sales");
                            return ValueTask.FromResult(new LoaderOutcome(Volatile.Read(ref sales)));
                        },
                        static (_, data) => new ViewKeyTests.Card((string)data!))
                    {
                        Receive = static (model, data) => ((ViewKeyTests.Card)model).Title = (string)data!,
                    },
                }),
            ],
        };
        await using var app = await StartAsync(site, [page]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var tab = await browser.NewPageAsync();

        var opened = tab.WaitForResponseAsync(response => response.Url.Contains("/live?plan=", StringComparison.Ordinal));
        await tab.GotoAsync(address + "/inbox");
        await opened;
        await tab.EvaluateAsync("() => { window.marker = 1; document.querySelector('#kept').value = 'typed'; }");
        Volatile.Write(ref sales, "after");
        app.Services.GetRequiredService<Runtime.Caching.DataVersions>().Invalidate("sales");
        await tab.WaitForFunctionAsync("() => document.querySelector('#value').textContent === 'after'").WaitAsync(TestContext.Current.CancellationToken);
        var kept = (await tab.EvaluateAsync<int?>("() => window.marker"), await tab.InputValueAsync("#kept"), await tab.TextContentAsync("h1"));

        Assert.Equal((1, "typed", "Inbox"), kept);
    }

    [Fact]
    [Trait("AC", "SSR-31")]
    public async Task NavigationsInTheSameLayoutReplaceOnlyThePageAsync()
    {
        var site = new Site();
        site.Engine.PlanFor = definition => new RenderPlan([], [
            new LiteralNode("<nav><a id=\"inbox\" href=\"/inbox\""), new CurrentNode("/inbox"), new LiteralNode(">Inbox</a><a id=\"other\" href=\"/other\""), new CurrentNode("/other"), new LiteralNode(">Other</a></nav>"),
            new LiteralNode(PageRegion.Open), new LiteralNode("<h1 data-route=\"" + definition.Route + "\">"), new ValueNode(scope => ((Model)scope!).Title), new LiteralNode("</h1>"), new LiteralNode(PageRegion.Close),
        ], []);
        PageDefinition Page(string route) => new(route, "Pages/inbox/Page.axaml", services => new Model("Page " + services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Path), []);
        await using var app = await StartAsync(site, [Page("/inbox"), Page("/other")]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var partial = new List<string>();
        page.Response += (_, response) => partial.Add(response.Url + " " + response.Headers.GetValueOrDefault("x-aioros-partial"));

        await page.GotoAsync(address + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; document.querySelector('nav').setAttribute('data-kept', '1'); }");
        await page.EvaluateAsync("() => document.querySelector('#other').click()");
        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'Page /other'");
        var state = await page.EvaluateAsync<string>("() => [window.marker, document.querySelector('nav').getAttribute('data-kept'), document.title, location.pathname, document.querySelector('#other').getAttribute('aria-current'), document.querySelector('#inbox').getAttribute('aria-current')].join('|')");

        Assert.Equal("1|1|Page /other|/other|page|", state);
        Assert.Contains(address + "/other 1", partial);
    }

    [Fact]
    [Trait("AC", "SSR-33")]
    public async Task CommandsNavigateWithoutReloadingThePageAsync()
    {
        var site = new Site();
        RenderPlan Titled(string tag) => new([], [new LiteralNode("<" + tag + ">"), new ValueNode(scope => ((IPageModel)scope!).Title), new LiteralNode("</" + tag + ">")], []);
        site.Engine.PlanFor = definition => Titled(definition.Route == "/inbox" ? "h2" : "h1");
        site.Engine.Command = models => ((Mover)models[^1]!).GoAsync(((Mover)models[^1]!).Title == "Moving" ? "inbox" : "?saved=1");
        PageDefinition Moving(string route, string title) => new(route, "Pages/move/Page.axaml", services => new Mover(
            services.GetRequiredService<Aioros.Navigation.INavigator>(),
            title + services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.QueryString.Value), []);
        await using var app = await StartAsync(site, [
            new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Inbox"), []),
            Moving("/move", "Moving"),
            Moving("/list", "List"),
        ]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answers = new List<string>();
        page.Response += (_, response) => answers.Add(response.Request.Method + " " + response.Url + " " + response.Headers.GetValueOrDefault("content-type"));

        await page.GotoAsync(address + "/move");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await page.EvaluateAsync(Button);
        await page.ClickAsync("#go");
        await page.WaitForFunctionAsync("() => document.querySelector('h2') && document.querySelector('h2').textContent === 'Inbox'");
        var moved = (await page.EvaluateAsync<int?>("() => window.marker"), page.Url);

        await page.GotoAsync(address + "/list");
        await page.WaitForFunctionAsync("() => performance.getEntriesByType('resource').some(entry => entry.name.includes('/_aioros/plan/'))");
        await page.EvaluateAsync("() => { window.marker = 2; }");
        await page.EvaluateAsync(Button);
        await page.ClickAsync("#go");
        await page.WaitForFunctionAsync("() => document.querySelector('h1').textContent === 'List?saved=1'");
        var stayed = (await page.EvaluateAsync<int?>("() => window.marker"), page.Url);

        Assert.Equal((1, address + "/inbox"), moved);
        Assert.Equal((2, address + "/list?saved=1"), stayed);
        Assert.Contains("GET " + address + "/list?saved=1 application/json", answers);
    }

    [Fact]
    [Trait("AC", "SSR-65")]
    public async Task CommandsThatLeaveTheSiteRunOnceAsync()
    {
        var site = new Site();
        var runs = 0;
        var elsewhere = "";
        site.Engine.Command = async models =>
        {
            Interlocked.Increment(ref runs);
            switch (models[^1])
            {
                case Mover mover:
                    await mover.GoAsync(elsewhere + "/landing?by=navigator");
                    break;
                case Model model:
                    model.RedirectTo = elsewhere + "/landing?by=redirect";
                    break;
            }
        };
        await using var app = await StartAsync(site, [
            new PageDefinition("/pay", "Pages/pay/Page.axaml", _ => new Model("Pay"), []),
            new PageDefinition("/go", "Pages/go/Page.axaml", services => new Mover(services.GetRequiredService<Aioros.Navigation.INavigator>()), []),
            new PageDefinition("/landing", "Pages/landing/Page.axaml", _ => new Model("Landing"), []),
        ]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        elsewhere = address.Replace("127.0.0.1", "localhost", StringComparison.Ordinal);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answers = new List<int>();
        page.Response += (_, response) =>
        {
            if (response.Request.Method == "POST")
            {
                answers.Add(response.Status);
            }
        };

        await page.GotoAsync(address + "/pay");
        await page.EvaluateAsync(Button);
        var redirected = page.WaitForRequestAsync(request => request.IsNavigationRequest && request.Url == elsewhere + "/landing?by=redirect");
        await page.ClickAsync("#go");
        await redirected;
        await page.WaitForFunctionAsync("url => location.href === url && document.readyState === 'complete'", elsewhere + "/landing?by=redirect");
        var redirectedRuns = Volatile.Read(ref runs);

        await page.GotoAsync(address + "/go");
        await page.EvaluateAsync(Button);
        var navigated = page.WaitForRequestAsync(request => request.IsNavigationRequest && request.Url == elsewhere + "/landing?by=navigator");
        await page.ClickAsync("#go");
        await navigated;
        await page.WaitForFunctionAsync("url => location.href === url && document.readyState === 'complete'", elsewhere + "/landing?by=navigator");
        var navigatedRuns = Volatile.Read(ref runs);

        Assert.Equal((1, 2), (redirectedRuns, navigatedRuns));
        Assert.Equal([200, 200], answers);
    }

    [Fact]
    [Trait("AC", "SSR-76")]
    public async Task AViewCommandMorphsOnlyItsRegionAsync()
    {
        const string Key = "Pages/inbox/Page.axaml:0.1";
        var site = new Site();
        var clicks = new Clicks();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<h1>"), new ValueNode(static model => ((Model)model!).Title), new LiteralNode("</h1>"),
            new ViewNode(1, Key, [new LiteralNode("<p class=\"count\">"), new ValueNode(static model => ((Clicks)model!).Count), new LiteralNode("</p><button id=\"add\" name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|x\">Add</button>")], []),
        ], []);
        site.Engine.Command = models =>
        {
            ((Clicks)models[1]!).Count++;
            return ValueTask.CompletedTask;
        };
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Inbox"), [])
        {
            Views = [new ViewInstance(Key, 0, new ViewDefinition("Views/Clicks.axaml", _ => clicks))],
        }]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answers = new List<string>();
        page.Response += (_, response) => answers.Add(response.Request.Method + " " + response.Headers.GetValueOrDefault("x-aioros-view"));

        await page.GotoAsync(address + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; const h = document.querySelector('h1'); h.setAttribute('data-kept', '1'); h.textContent = 'Changed here'; }");
        await page.ClickAsync("#add");
        await page.WaitForFunctionAsync("() => document.querySelector('.count').textContent === '1'");
        var state = await page.EvaluateAsync<string>("() => [window.marker, document.querySelector('h1').getAttribute('data-kept'), document.querySelector('h1').textContent].join('|')");
        var region = await page.EvaluateAsync<string>("key => document.querySelector(`[data-aioros-view=\"${key}\"]`).outerHTML", Key);
        var expected = await page.EvaluateAsync<string>("async key => new DOMParser().parseFromString(await (await fetch(location.href)).text(), 'text/html').querySelector(`[data-aioros-view=\"${key}\"]`).outerHTML", Key);

        Assert.Equal("1|1|Changed here", state);
        Assert.Contains("POST " + Key, answers);
        Assert.Equal(expected, region);
        Assert.Equal(1, clicks.Count);
    }

    [Fact]
    [Trait("AC", "SSR-82")]
    public async Task ACommandMorphsEveryRegionItsKeysRefreshedAsync()
    {
        const string List = "Pages/inbox/Page.axaml:0.1";
        const string Form = "Pages/inbox/Page.axaml:0.2";
        var site = new Site();
        var items = System.Collections.Immutable.ImmutableList.Create("first");
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<h1>"), new ValueNode(static model => ((Model)model!).Title), new LiteralNode("</h1>"),
            new ViewNode(1, List, [new LiteralNode("<ul class=\"list\">"), new RepeatNode(static model => (System.Collections.IEnumerable)model!, [new LiteralNode("<li>"), new ValueNode(static item => item), new LiteralNode("</li>")]), new LiteralNode("</ul>")], []),
            new ViewNode(2, Form, [new LiteralNode("<p class=\"form\">"), new ValueNode(static model => ((Model)model!).Title), new LiteralNode("</p><button id=\"add\" name=\"_btn\" value=\""), IdNode.Instance, new LiteralNode("|x\">Add</button>")], []),
        ], []);
        site.Engine.Command = _ =>
        {
            items = items.Add("second");
            Runtime.Functions.DataKeys.Ambient.Invalidate("items");
            return ValueTask.CompletedTask;
        };
        LoaderDefinition Loader(Func<IServiceProvider, object> load, Func<object?, object?> create) =>
            new((services, _, _) => ValueTask.FromResult(new LoaderOutcome(load(services))), (_, data) => create(data));
        await using var app = await StartAsync(site, [new PageDefinition("/inbox", "Pages/inbox/Page.axaml", _ => new Model("Inbox"), [])
        {
            Views =
            [
                new ViewInstance(List, 0, new ViewDefinition("Views/List.axaml", _ => null)
                {
                    Loader = Loader(services =>
                    {
                        services.GetRequiredService<Aioros.Functions.IDataKeys>().Read("items");
                        return items;
                    }, static data => data),
                }),
                new ViewInstance(Form, 0, new ViewDefinition("Views/Form.axaml", _ => null) { Loader = Loader(_ => items.Count + " items", static data => new Model((string)data!)) }),
            ],
        }]);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var answers = new List<string>();
        page.Response += (_, response) => answers.Add(response.Request.Method + " " + response.Headers.GetValueOrDefault("x-aioros-view"));

        await page.GotoAsync(address + "/inbox");
        await page.EvaluateAsync("() => { window.marker = 1; document.querySelector('h1').setAttribute('data-kept', '1'); }");
        await page.ClickAsync("#add");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.list li').length === 2");
        var state = await page.EvaluateAsync<string>("() => [window.marker, document.querySelector('h1').getAttribute('data-kept'), [...document.querySelectorAll('.list li')].map(li => li.textContent).join(','), document.querySelector('.form').textContent].join('|')");
        const string Regions = "() => [...document.querySelectorAll('[data-aioros-view]')].map(region => region.outerHTML).join('')";
        var regions = await page.EvaluateAsync<string>(Regions);
        var expected = await page.EvaluateAsync<string>("async () => [...new DOMParser().parseFromString(await (await fetch(location.href)).text(), 'text/html').querySelectorAll('[data-aioros-view]')].map(region => region.outerHTML).join('')");

        Assert.Equal("1|1|first,second|2 items", state);
        Assert.Contains("POST " + List + ", " + Form, answers);
        Assert.Equal(expected, regions);
    }

    private sealed class Clicks
    {
        public int Count { get; set; }
    }

    private static async Task<WebApplication> StartAsync(Site site, IReadOnlyList<PageDefinition> pages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor().AddSingleton<Runtime.Caching.DataVersions>()
            .AddScoped<Runtime.Pages.PageNavigator>().AddScoped<Aioros.Navigation.INavigator>(services => services.GetRequiredService<Runtime.Pages.PageNavigator>());
        var app = builder.Build();
        app.MapAiorosPages(pages);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
