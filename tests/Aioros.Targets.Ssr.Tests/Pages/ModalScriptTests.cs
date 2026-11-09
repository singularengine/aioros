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

public sealed class ModalScriptTests
{
    [Fact]
    [Trait("AC", "OVERLAY-12")]
    public async Task MarkedDialogsOpenAndCloseInTheBrowserAsync()
    {
        var model = new Editing();
        var site = new Site();
        site.Engine.Command = _ =>
        {
            var form = site.Engine.Posted!;
            model.Open = form.GetValueOrDefault("_btn") switch
            {
                "0|" => true,
                "1|" => false,
                _ => form.ContainsKey("f0"),
            };
            model.Index++;
            return ValueTask.CompletedTask;
        };
        static IPlanNode When(bool open, string text) => new ConditionNode(model => ((Editing)model!).Open == open, [new LiteralNode(text)]);
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<style>dialog.prompt[open]{display:grid;inset:0;width:100%;height:100%;max-width:none;max-height:none;margin:0;padding:0;border:0;background:none}fieldset.mf{display:contents}.pm{grid-area:1/1}.sf{grid-area:1/1;place-self:center;background:#fff;padding:24px}</style>"
                + "<i id=\"step\" data-n=\""), new ValueNode(static model => ((Editing)model!).Index), new LiteralNode("\"></i>"
                + "<button type=\"button\" id=\"t\" commandfor=\"dlg1\" command=\"show-modal\">Edit</button>"
                + "<dialog id=\"dlg1\" class=\"prompt\" data-aioros-modal=\"_s0\" data-aioros-dismiss data-change data-aioros-known"), When(true, " open"),
            new LiteralNode("><input type=\"checkbox\" hidden name=\"f0\""), When(true, " checked"), new LiteralNode("><fieldset class=\"mf\""), When(false, " disabled"),
            new LiteralNode("><div class=\"pm\" id=\"mask\"></div><div class=\"sf\"><input id=\"name\" name=\"f1\" value=\"Ada\"><button name=\"_btn\" value=\"1|\" id=\"save\">Save</button>"
                + "<button type=\"button\" id=\"close\" commandfor=\"dlg1\" command=\"close\">Close</button></div></fieldset></dialog>"
                + "<button type=\"button\" id=\"t2\" commandfor=\"dlg2\" command=\"show-modal\">Open 2</button>"
                + "<dialog id=\"dlg2\" class=\"prompt\" data-aioros-modal=\"_s1\"><fieldset class=\"mf\" disabled><div class=\"pm\" id=\"mask2\"></div><div class=\"sf\">"
                + "<button type=\"button\" id=\"close2\" commandfor=\"dlg2\" command=\"close\">Close 2</button></div></fieldset></dialog>"
                + "<button name=\"_btn\" value=\"0|\" id=\"next\">Next</button><input type=\"hidden\" name=\"_s0\" value=\""), When(true, "True"), When(false, "False"),
            new LiteralNode("\"><input type=\"hidden\" name=\"_s1\" value=\"False\">"),
        ], []);
        await using var app = await HostAsync(site, model);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var module = page.WaitForResponseAsync(response => response.Url.Contains(ModalScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/modal");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", ModalScript.Path);
        var posts = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posts.Add(Uri.UnescapeDataString(request.PostData ?? string.Empty));
            }
        };
        const string State = "id => { const d = document.getElementById(id), s = document.getElementsByName(d.dataset.aiorosModal)[0], c = d.querySelector(':scope>input[type=checkbox]'); "
            + "return [d.open && d.matches(':modal'), s.value, c ? c.checked : '-', d.contains(document.activeElement) ? 'in' : document.activeElement?.id || '-', d.querySelector('fieldset').disabled].join() }";
        async Task SyncedAsync(string id, string state) => await page.WaitForFunctionAsync("([id, s]) => document.getElementsByName(document.getElementById(id).dataset.aiorosModal)[0].value === s", new[] { id, state }, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);
        async Task StepAsync(int n) => await page.WaitForFunctionAsync("n => document.getElementById('step').dataset.n === String(n)", n, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);

        var start = await page.EvaluateAsync<string>(State, "dlg1");
        var hidden = await page.GetByRole(AriaRole.Button, new() { Name = "Close 2" }).CountAsync();
        await page.ClickAsync("#t");
        await SyncedAsync("dlg1", "True");
        var opened = await page.EvaluateAsync<string>(State, "dlg1");
        await StepAsync(1);
        var kept = await page.EvaluateAsync<string>(State, "dlg1");
        await page.ClickAsync("#close");
        await SyncedAsync("dlg1", "False");
        var closed = await page.EvaluateAsync<string>(State, "dlg1");
        await StepAsync(2);
        await page.ClickAsync("#t");
        await StepAsync(3);
        await page.Keyboard.PressAsync("Escape");
        await StepAsync(4);
        var escaped = await page.EvaluateAsync<string>(State, "dlg1");
        await page.ClickAsync("#t");
        await StepAsync(5);
        await page.Mouse.ClickAsync(5, 5);
        await StepAsync(6);
        var dismissed = await page.EvaluateAsync<string>(State, "dlg1");
        var sent = posts.Count;
        await page.ClickAsync("#t2");
        await SyncedAsync("dlg2", "True");
        await page.Mouse.ClickAsync(5, 5);
        var stays = await page.EvaluateAsync<string>(State, "dlg2");
        await page.ClickAsync("#close2");
        await SyncedAsync("dlg2", "False");
        var unposted = (await page.EvaluateAsync<string>(State, "dlg2"), posts.Count - sent);
        await page.ClickAsync("#next");
        await StepAsync(7);
        var commanded = await page.EvaluateAsync<string>(State, "dlg1");
        await page.ClickAsync("#save");
        await StepAsync(8);
        var saved = await page.EvaluateAsync<string>(State, "dlg1");

        Assert.Equal(("false,False,false,-,true", 0), (start, hidden));
        Assert.Equal("true,True,true,in,false", opened);
        Assert.Equal(opened, kept);
        Assert.Contains("_chg=0", posts[0], StringComparison.Ordinal);
        Assert.Contains("f0=on", posts[0], StringComparison.Ordinal);
        Assert.Equal("false,False,false,t,true", closed);
        Assert.Equal("false,False,false,t,true", escaped);
        Assert.Equal("false,False,false,t,true", dismissed);
        Assert.Equal("true,True,-,in,false", stays);
        Assert.Equal(("false,False,-,t2,true", 0), unposted);
        Assert.Equal("true,True,true,in,false", commanded);
        Assert.StartsWith("false,False,false,", saved, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "OVERLAY-19")]
    public async Task AKeyedDialogOpensAndClosesWithItsKeyAsync()
    {
        var model = new Editing();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<style>dialog.prompt[open]{display:grid}fieldset.mf{display:contents}</style><input id=\"search\" name=\"f0\">"
                + "<dialog id=\"dlg1\" class=\"prompt\" data-aioros-modal=\"_s0\" data-aioros-key=\"ctrl+KeyK\" aria-keyshortcuts=\"Control+K\"><fieldset class=\"mf\" disabled><input id=\"inside\" name=\"f1\"></fieldset></dialog>"
                + "<input type=\"hidden\" name=\"_s0\" value=\"False\">"),
        ], []);
        await using var app = await HostAsync(site, model);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var modal = page.WaitForResponseAsync(response => response.Url.Contains(ModalScript.Prefix, StringComparison.Ordinal));
        var keys = page.WaitForResponseAsync(response => response.Url.Contains(KeyboardScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/modal");
        await Task.WhenAll(modal, keys).WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("([a, b]) => Promise.all([import(a), import(b)]).then(() => 0)", new[] { ModalScript.Path, KeyboardScript.Path });
        var requests = 0;
        page.Request += (_, request) => requests += request.Method == "POST" ? 1 : 0;
        const string State = "() => { const d = document.getElementById('dlg1'); return [d.open && d.matches(':modal'), document.getElementsByName('_s0')[0].value].join() }";
        async Task SyncedAsync(string state) => await page.WaitForFunctionAsync("s => document.getElementsByName('_s0')[0].value === s", state, new() { PollingInterval = 20 }).WaitAsync(TestContext.Current.CancellationToken);

        await page.FocusAsync("#search");
        await page.Keyboard.PressAsync("Control+KeyK");
        await SyncedAsync("True");
        var opened = await page.EvaluateAsync<string>(State);
        await page.FocusAsync("#inside");
        await page.Keyboard.PressAsync("Meta+KeyK");
        await SyncedAsync("False");
        var closed = await page.EvaluateAsync<string>(State);

        Assert.Equal(("true,True", "false,False", 0), (opened, closed, requests));
    }

    [Fact]
    [Trait("AC", "OVERLAY-13")]
    public async Task ADialogWhoseSurfaceSlidesInKeepsTheFocusOnItsFieldAsync()
    {
        var model = new Editing();
        var site = new Site();
        site.Engine.Plan = new RenderPlan([], [
            new LiteralNode("<style>dialog.prompt[open]{display:grid;inset:0;width:100%;height:100%;max-width:none;max-height:none;margin:0;padding:0;border:0;background:none}fieldset.mf{display:contents}"
                + ".wrap{grid-area:1/1;overflow:auto;justify-self:end;align-self:stretch}.sheet{width:300px;height:100%;background:#fff;transition:transform 200ms}@starting-style{.sheet{transform:translateX(100%)}}</style>"
                + "<button type=\"button\" id=\"t\" commandfor=\"dlg1\" command=\"show-modal\">Settings</button>"
                + "<dialog id=\"dlg1\" class=\"prompt\" data-aioros-modal=\"_s0\"><fieldset class=\"mf\" disabled><div class=\"wrap\"><div class=\"sheet\"><input id=\"name\" name=\"f0\" value=\"Ada\"></div></div></fieldset></dialog>"
                + "<input type=\"hidden\" name=\"_s0\" value=\"False\">"),
        ], []);
        await using var app = await HostAsync(site, model);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 600 } });
        var module = page.WaitForResponseAsync(response => response.Url.Contains(ModalScript.Prefix, StringComparison.Ordinal));
        await page.GotoAsync(Address(app) + "/modal");
        await module.WaitAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("u => import(u).then(() => 0)", ModalScript.Path);

        await page.ClickAsync("#t");
        await page.WaitForFunctionAsync("() => document.getElementById('dlg1').matches(':modal') && document.getAnimations().every((animation) => animation.playState !== 'running')", null, new() { PollingInterval = 20 });
        var focused = await page.EvaluateAsync<string>("() => document.activeElement?.id || document.activeElement?.tagName");

        Assert.Equal("name", focused);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private static async Task<WebApplication> HostAsync(Site site, Editing model)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site.Handler).AddSingleton(site.Cache).AddSingleton(site.Options).AddHttpContextAccessor();
        var app = builder.Build();
        app.MapAiorosPages([new PageDefinition("/modal", "Pages/modal/Page.axaml", _ => model, [])]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Editing : IPageModel
    {
        public string Title => "Modal";

        public int Index { get; set; }

        public bool Open { get; set; }
    }
}
