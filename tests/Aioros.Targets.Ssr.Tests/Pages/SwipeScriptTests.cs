using Aioros.Targets.Ssr.Documents;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class SwipeScriptTests
{
    private const string Css = """
        <style>dialog.d{inset:0;margin:0;padding:0;border:0;width:100vw;height:100vh;max-width:none;max-height:none;background:none;display:none}dialog.d[open]{display:block}
        .pm{position:absolute;inset:0;background:rgb(0 0 0/.5);transition:opacity .2s}.sf{position:absolute;left:0;right:0;bottom:0;height:300px;background:#fff;transition:translate .5s cubic-bezier(.32,.72,0,1)}</style>
        """;

    private const string Surface = "() => { const s = document.getElementById('sf'), m = document.querySelector('.pm'); return [s.style.translate || 'rest', s.style.transition || '-', m.style.opacity || '1', document.getElementById('dlg').open, document.getElementsByName('_s0')[0].value].join('|'); }";

    private const string Rest = "() => Math.abs(document.getElementById('sf').getBoundingClientRect().top - 300) < 0.5";

    private const string Closed = "() => !document.getElementById('dlg').open && document.getElementsByName('_s0')[0].value === 'False'";

    private static string Page(string by) => Css
        + "<input type=\"hidden\" name=\"_s0\" value=\"True\"><dialog id=\"dlg\" class=\"d\" data-aioros-modal=\"_s0\"><div class=\"pm\"></div>"
        + $"<div id=\"sf\" class=\"sf\" data-aioros-swipe=\"down\" data-aioros-swipe-by=\"{by}\"><p>Drawer</p></div></dialog>";

    [Fact]
    [Trait("AC", "SSR-249")]
    public async Task ADialogsSurfaceDraggedDownClosesItAsync()
    {
        await using var stage = await GestureStage.StartAsync(_ => Page("0.25 1000"), SwipeScript.Prefix);
        var page = stage.Page;
        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");
        await page.WaitForFunctionAsync("() => document.getElementsByName('_s0')[0].value === 'True' && document.getElementById('dlg').open", null, new() { PollingInterval = 20 });
        var top = 600 - 300 + 20;
        await page.Mouse.MoveAsync(400, top);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(400, top + 60, new() { Steps = 6 });
        var dragging = await page.EvaluateAsync<string>(Surface);
        await page.Mouse.UpAsync();
        var back = await page.EvaluateAsync<string>(Surface);
        await page.WaitForFunctionAsync(Rest, null, new() { PollingInterval = 20 });
        await page.Mouse.MoveAsync(400, top);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(400, top - 30, new() { Steps = 6 });
        var up = await page.EvaluateAsync<string>(Surface);
        await page.Mouse.UpAsync();
        var down = await page.EvaluateAsync<string>(Surface);
        await page.WaitForFunctionAsync(Rest, null, new() { PollingInterval = 20 });
        await page.Mouse.MoveAsync(400, top);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(400, top + 100, new() { Steps = 8 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync(Closed, null, new() { PollingInterval = 20 });
        var closed = await page.EvaluateAsync<string>(Surface);
        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");
        await page.WaitForFunctionAsync("() => document.getElementsByName('_s0')[0].value === 'True'", null, new() { PollingInterval = 20 });
        var reopened = await page.EvaluateAsync<string>(Surface);
        await using var fast = await GestureStage.StartAsync(_ => Page("0.25 0.001"), SwipeScript.Prefix);
        page = fast.Page;
        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");
        await page.WaitForFunctionAsync("() => document.getElementsByName('_s0')[0].value === 'True'", null, new() { PollingInterval = 20 });
        await page.Mouse.MoveAsync(400, top);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(400, top + 10, new() { Steps = 2 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync(Closed, null, new() { PollingInterval = 20 });
        var flicked = await page.EvaluateAsync<string>(Surface);

        Assert.Equal("0px 60px|none|0.8|true|True", dragging);
        Assert.Equal("rest|-|1|true|True", back);
        Assert.Equal("0px -11.4719px|none|1|true|True", up);
        Assert.Equal("rest|-|1|true|True", down);
        Assert.Equal("0px 100px|-|1|false|False", closed);
        Assert.Equal("rest|-|1|true|True", reopened);
        Assert.Equal("0px 10px|-|1|false|False", flicked);
    }
}
