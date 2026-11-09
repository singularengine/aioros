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

public sealed class PromptScriptTests
{
    private const string Page = "<p id=\"state\">idle</p><input type=\"text\" name=\"f0\" value=\"Ada\"><button type=\"submit\" name=\"_btn\" value=\"0|\">Ask</button>";

    private const string Prompt =
        "<dialog open class=\"prompt\"><input type=\"hidden\" name=\"_prompt\" value=\"_btn=0%7C\"><button type=\"submit\" name=\"_btn\" value=\"1|\" id=\"ok\">OK</button></dialog>";

    private const string Shown = """
        window.shown = [];
        const modal = HTMLDialogElement.prototype.showModal, popover = HTMLElement.prototype.showPopover;
        HTMLDialogElement.prototype.showModal = function () { window.shown.push('modal'); return modal.call(this); };
        HTMLElement.prototype.showPopover = function () { window.shown.push('popover'); return popover.call(this); };
        """;

    private const string Notice ="<div popover=\"manual\" class=\"notice\" id=\"toast\">Saved</div>";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-154")]
    public async Task APromptIsShownModalAndItsButtonsAnswerItAsync()
    {
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = Recording(site, posted, Page + Prompt, Page.Replace("idle", "done", StringComparison.Ordinal));
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(Address(app) + "/ask");
        await page.EvaluateAsync("() => { window.marker = 1; }");
        await page.ClickAsync("text=Ask");
        await page.WaitForFunctionAsync("() => !!document.querySelector('dialog.prompt:modal')").WaitAsync(TestContext.Current.CancellationToken);
        await page.ClickAsync("#ok");
        await page.WaitForFunctionAsync("() => document.querySelector('#state').textContent === 'done'").WaitAsync(TestContext.Current.CancellationToken);
        var scripted = (await page.EvaluateAsync<int?>("() => window.marker"), await page.EvaluateAsync<int>("() => document.querySelectorAll('dialog').length"));
        var sent = posted.ToList();

        posted.Clear();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var still = await context.NewPageAsync();
        await still.GotoAsync(Address(app) + "/ask");
        await still.ClickAsync("text=Ask");
        var visible = await still.IsVisibleAsync("#ok");
        await still.ClickAsync("#ok");

        Assert.Equal((1, 0), scripted);
        Assert.Equal(["_btn=0|&f0=Ada", "_btn=1|&_prompt=_btn=0%7C&f0=Ada"], sent);
        Assert.True(visible);
        Assert.Equal(["_btn=0|&f0=Ada", "_btn=1|&_prompt=_btn=0%7C&f0=Ada"], posted);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-155")]
    public async Task NoticesAreShownAsPopoversAboveAPromptAsync()
    {
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = Recording(site, posted, Page + Prompt + Notice, Page + "<div popover=\"manual\" class=\"notice\" id=\"closing\"><button type=\"submit\" name=\"_btn\" value=\"2|\" id=\"close\">x</button></div>"
            + "<div popover=\"manual\" class=\"notice\" id=\"acting\" data-replay=\"_btn=0%7C\" style=\"inset:auto;top:200px;left:0\"><button type=\"submit\" name=\"_btn\" value=\"3|\" id=\"undo\">Undo</button></div>");
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        await page.AddInitScriptAsync(Shown);

        await page.GotoAsync(Address(app) + "/ask");
        await page.ClickAsync("text=Ask");
        await page.WaitForFunctionAsync("() => !!document.querySelector('#toast:popover-open') && !!document.querySelector('dialog.prompt:modal')").WaitAsync(TestContext.Current.CancellationToken);
        var order = await page.EvaluateAsync<string>("() => window.shown.join()");
        await page.ClickAsync("#ok");
        await page.WaitForFunctionAsync("() => !!document.querySelector('#closing:popover-open')").WaitAsync(TestContext.Current.CancellationToken);
        await page.ClickAsync("#close");
        var closed = await page.EvaluateAsync<bool>("() => !document.querySelector('#closing').matches(':popover-open')");
        var quiet = posted.Count;
        var undone = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#undo");
        await undone;

        Assert.Equal("modal,popover", order);
        Assert.True(closed);
        Assert.Equal(2, quiet);
        Assert.Equal("_btn=3|&_prompt=_btn=0%7C&f0=Ada", posted[^1]);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-236")]
    public async Task AnAnswerLoadedBeforeTheScriptShowsItsOverlaysWhenTheScriptStartsAsync()
    {
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = Recording(site, posted, Page + Prompt + Notice, Page);
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var loads = 0;
        await page.RouteAsync("**" + Scripts.Path + "*", async route =>
        {
            if (Interlocked.Increment(ref loads) == 1)
            {
                await route.AbortAsync();
                return;
            }

            await route.ContinueAsync();
        });
        var bound = TestContext.Current.CancellationToken;

        await page.GotoAsync(Address(app) + "/ask");
        var before = await page.EvaluateAsync<bool>("() => window.aioros === undefined");
        var answered = page.WaitForRequestFinishedAsync(new() { Predicate = request => request.Method == "POST" });
        await page.ClickAsync("text=Ask");
        await answered;
        await page.WaitForFunctionAsync("() => !!document.querySelector('dialog.prompt:modal') && !!document.querySelector('#toast:popover-open')").WaitAsync(bound);
        await page.GotoAsync(Address(app) + "/ask");
        await page.WaitForFunctionAsync("() => window.aioros !== undefined").WaitAsync(bound);
        var plain = await page.EvaluateAsync<int>("() => document.querySelectorAll('dialog,.notice,:modal,:popover-open').length");

        Assert.True(before);
        Assert.Equal(["_btn=0|&f0=Ada"], posted);
        Assert.Equal(0, plain);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-238")]
    public async Task ANoticesButtonCarriesItsStepWithoutTheScriptAsync()
    {
        const string Acting = "<style>@media (scripting:none){.notice[popover]{display:block}}</style><div popover=\"manual\" class=\"notice\" data-replay=\"_btn=0%7C\"><input type=\"hidden\" name=\"_notice\" value=\"3&#10;_btn=0%7C\">"
            + "<button type=\"submit\" name=\"_btn\" value=\"3|\" id=\"undo\">Undo</button></div>";
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(string.Join("&", form.Where(pair => pair.Key != "__RequestVerificationToken").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(Page)], []) }) { Shows = true };
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Page + Acting)], []);
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;

        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.ClickAsync("#undo").WaitAsync(bound);
        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.ClickAsync("text=Ask").WaitAsync(bound);

        Assert.Equal(["_btn=3|&_prompt=_btn=0%7C&f0=Ada", "_btn=0|&f0=Ada"], posted);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-239")]
    public async Task APageButtonPressedWhileAPromptIsOpenCarriesNoStepsWithoutTheScriptAsync()
    {
        const string Open = "<dialog open class=\"prompt\"><input type=\"hidden\" name=\"_prompt\" value=\"_btn=0%7C\"><button type=\"submit\" name=\"_btn\" value=\"1|\" id=\"ok\">OK</button></dialog>"
            + "<input type=\"hidden\" name=\"_answers\" value=\"1\">";
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(string.Join("&", form.Where(pair => pair.Key != "__RequestVerificationToken").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(Page)], []) }) { Shows = true };
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Page + Open)], []);
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;

        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.ClickAsync("text=Ask").WaitAsync(bound);
        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.ClickAsync("#ok").WaitAsync(bound);

        Assert.Equal(["_btn=0|&f0=Ada", "_btn=1|&_prompt=_btn=0%7C&f0=Ada"], posted);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-240")]
    public async Task APageButtonPressedWhileALibraryDialogIsOpenCarriesNoStepsWithoutTheScriptAsync()
    {
        const string Open = "<dialog id=\"dlg1\" class=\"prompt\" data-aioros-modal=\"_s0\" data-aioros-known open><input type=\"hidden\" name=\"_s0\" value=\"True\">"
            + "<fieldset class=\"mf\"><button type=\"submit\" name=\"_btn\" value=\"1|\" id=\"save\">Save</button></fieldset></dialog>"
            + "<input type=\"hidden\" name=\"_prompt\" value=\"_btn=0%7C\"><input type=\"hidden\" name=\"_answers\" value=\"1\">";
        var site = new Site();
        var posted = new List<string>();
        site.Engine.Command = _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(string.Join("&", form.Where(pair => pair.Key != "__RequestVerificationToken").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(Page)], []) }) { Shows = true };
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Page + Open)], []);
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;

        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.ClickAsync("text=Ask").WaitAsync(bound);
        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.ClickAsync("#save").WaitAsync(bound);

        Assert.Equal(["_btn=0|&_s0=True&f0=Ada", "_btn=1|&_prompt=_btn=0%7C&_s0=True&f0=Ada"], posted);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-197")]
    public async Task APromptsEscapeAndMaskPostItsDismissalAsync()
    {
        var site = new Site();
        var posted = new List<string>();
        const string Masked = "<dialog open class=\"prompt\"><input type=\"hidden\" name=\"_prompt\" value=\"_btn=0%7C\"><div class=\"pm\" style=\"position:fixed;inset:0;background:#0008\"></div>"
            + "<div id=\"surface\" style=\"position:relative;background:#fff;width:200px;height:100px\">Surface</div></dialog>";
        site.Engine.Command = _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(string.Join("&", form.Where(pair => pair.Key != "__RequestVerificationToken").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            var keep = form.TryGetValue("_dismiss", out var dismissal) && dismissal == "outside";
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(form.ContainsKey("_prompt") && !keep ? Page : Page + Masked)], []) }) { Shows = !form.ContainsKey("_prompt") || keep };
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Page)], []);
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;

        await page.GotoAsync(Address(app) + "/ask");
        await page.ClickAsync("text=Ask");
        await page.WaitForFunctionAsync("() => !!document.querySelector('dialog.prompt:modal')").WaitAsync(bound);
        await page.ClickAsync("#surface");
        var surface = posted.Count;
        var outside = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.Mouse.ClickAsync(5, 5);
        await outside;
        await page.WaitForFunctionAsync("() => !!document.querySelector('dialog.prompt:modal')").WaitAsync(bound);
        var escaped = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.Keyboard.PressAsync("Escape");
        await escaped;
        await page.WaitForFunctionAsync("() => !document.querySelector('dialog')").WaitAsync(bound);

        Assert.Equal(1, surface);
        Assert.Equal(["_btn=0|&f0=Ada", "_dismiss=outside&_prompt=_btn=0%7C&f0=Ada", "_dismiss=escape&_prompt=_btn=0%7C&f0=Ada"], posted);
    }

    private static Func<System.Collections.Immutable.ImmutableArray<object?>, ValueTask> Recording(Site site, List<string> posted, string prompted, string answered)
    {
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Page)], []);
        return _ =>
        {
            var form = site.Engine.Posted!;
            posted.Add(string.Join("&", form.Where(pair => pair.Key != "__RequestVerificationToken").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
            var shown = !form.ContainsKey("_prompt");
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(shown ? prompted : answered)], []) }) { Shows = shown };
            return ValueTask.CompletedTask;
        };
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-242")]
    public async Task APopoverWrittenOpenStaysOpenAcrossAnswersAsync()
    {
        static string Markup(int answer) => $"<i id=\"n\" data-n=\"{answer}\"></i><button type=\"button\" id=\"t\" popovertarget=\"p\">Open</button><div popover id=\"p\" data-aioros-open><button type=\"submit\" name=\"_btn\" value=\"0|\" id=\"go\">Go</button></div>";
        var site = new Site();
        var answers = 0;
        site.Engine.Command = _ =>
        {
            answers++;
            site.Engine.Outcome = new PostOutcome(PostStatus.Rendered, FakeEngine.Page(reusable: false) with { Plan = new RenderPlan([], [new LiteralNode(Markup(answers))], []) });
            return ValueTask.CompletedTask;
        };
        site.Engine.Plan = new RenderPlan([], [new LiteralNode(Markup(0))], []);
        await using var app = await StartAsync(site);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync();
        var bound = TestContext.Current.CancellationToken;
        await page.GotoAsync(Address(app) + "/ask").WaitAsync(bound);
        await page.WaitForFunctionAsync("() => window.aioros && document.getElementById('p').matches(':popover-open')").WaitAsync(bound);
        var open = new List<bool>();
        for (var answer = 1; answer <= 2; answer++)
        {
            await page.ClickAsync("#go").WaitAsync(bound);
            await page.WaitForFunctionAsync($"() => document.getElementById('n').dataset.n === '{answer}'").WaitAsync(bound);
            open.Add(await page.EvaluateAsync<bool>("() => document.getElementById('p').matches(':popover-open')"));
        }

        Assert.Equal([true, true], open);
    }

    private static async Task<WebApplication> StartAsync(Site site)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/ask", "Pages/ask/Page.axaml", _ => new Asked(), [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Asked : IPageModel
    {
        public string Title => "Ask";
    }
}
