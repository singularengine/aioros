using Aioros.Targets.Ssr.Documents;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class DeckScriptTests
{
    private const string Css = """
        <style>
        .notice[popover]{position:fixed;inset:auto;margin:0;border:0;padding:0;background:none;overflow:visible}.notice.na:popover-open{display:block}
        .notice>.nt{animation:notice-out .3s ease forwards;animation-delay:var(--expire)}@keyframes notice-out{to{opacity:0;visibility:hidden}}
        .notice.nd>.nt{position:absolute;left:0;right:0;bottom:0;transition:translate .4s,scale .4s,opacity .4s,height .4s}.notice.nd>.nt>*{height:100%;box-sizing:border-box}
        .notice.nd>.nt[data-aioros-swiping]{transition:none!important}.card{background:#fff;border:1px solid #ccc}
        @keyframes deck-out-right{from{transform:translate(var(--sx,0px),var(--sy,0px))}to{transform:translate(calc(var(--sx,0px) + 100%),var(--sy,0px));opacity:0}}
        .notice>.nt[data-out]{animation-duration:.2s!important;animation-timing-function:ease-out!important;animation-fill-mode:forwards!important;animation-delay:0s!important;animation-play-state:running!important}
        .notice>.nt[data-out=right]{animation-name:deck-out-right!important}
        </style>
        """;

    private const string Stack = """
        () => { const g = document.querySelector('.notice[data-aioros-deck]'); return !g ? 'none' : g.style.height + ' ' + [...g.querySelectorAll(':scope > .nt:not([data-gone])')]
          .map(x => [x.style.translate, x.style.scale || '1', x.style.height || 'auto', x.hasAttribute('data-behind') ? 'b' : 'f', x.getAnimations().find(a => a.animationName)?.playState ?? '-'].join('|')).join(' / '); }
        """;

    private static string Card(string text, int height, int expire, string inside = "", string attributes = "") =>
        $"<div class=\"nt\" role=\"status\" data-h=\"{height}\"{attributes} style=\"--expire:{expire}ms\"><div class=\"card\" style=\"position:relative\"><div style=\"height:{height - 2}px\">{text}</div><div style=\"position:absolute;top:0;right:0\">{inside}</div></div></div>";

    private static string Deck(string cards, string attributes = "") =>
        $"<div popover=\"manual\" class=\"notice na nd\" data-aioros-deck=\"14 3 0.05\" data-aioros-anchor=\"bottom\"{attributes} style=\"bottom:24px;right:24px;width:300px;height:70px\">{cards}</div>";

    [Fact]
    [Trait("AC", "SSR-245")]
    public async Task ADeckExpandsUnderThePointerAndStopsItsExpiryAsync()
    {
        await using var stage = await GestureStage.StartAsync(_ => Css + Deck(Card("Oldest", 60, 60000) + Card("Middle", 50, 60000).Replace("data-h=\"50\"", "data-h=\"40\"", StringComparison.Ordinal) + Card("Newest", 70, 60000))
            + "<div style=\"position:fixed;left:0;top:0;width:100px;height:100px\" id=\"away\"></div>", SwipeScript.Prefix);
        var page = stage.Page;
        await page.WaitForFunctionAsync("() => document.querySelector('.notice').matches(':popover-open')", null, new() { PollingInterval = 20 });
        await page.Mouse.MoveAsync(10, 10);
        var rest = await page.EvaluateAsync<string>(Stack);
        await page.HoverAsync(".notice .nt:last-child");
        var over = await page.EvaluateAsync<string>(Stack);
        await page.Mouse.MoveAsync(10, 10);
        var left = await page.EvaluateAsync<string>(Stack);
        await page.HoverAsync(".notice .nt:last-child");
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(10, 10);
        var pressed = await page.EvaluateAsync<string>(Stack);
        await page.Mouse.UpAsync();
        var released = await page.EvaluateAsync<string>(Stack);
        await page.EvaluateAsync("() => { Object.defineProperty(document, 'hidden', { value: true, configurable: true }); document.dispatchEvent(new Event('visibilitychange')) }");
        var hidden = await page.EvaluateAsync<string>(Stack);
        await page.EvaluateAsync("() => { Object.defineProperty(document, 'hidden', { value: false, configurable: true }); document.dispatchEvent(new Event('visibilitychange')) }");
        await page.EvaluateAsync("() => { const g = document.querySelector('.notice'); g.setAttribute('data-aioros-expanded', ''); document.dispatchEvent(new Event('visibilitychange')) }");
        var always = await page.EvaluateAsync<string>(Stack);

        const string Collapsed = "70px 0px -28px|0.9|70px|b|running / 0px -14px|0.95|70px|b|running / 0px|1|auto|f|running";
        const string Expanded = "208px 0px -148px|1|auto|f|paused / 0px -84px|1|auto|f|paused / 0px|1|auto|f|paused";
        Assert.Equal(Collapsed, rest);
        Assert.Equal(Expanded, over);
        Assert.Equal(Collapsed, left);
        Assert.Equal(Expanded, pressed);
        Assert.Equal(Collapsed, released);
        Assert.Equal(Collapsed.Replace("running", "paused", StringComparison.Ordinal), hidden);
        Assert.Equal(Expanded, always);
    }

    [Fact]
    [Trait("AC", "SSR-246")]
    public async Task ADeckTakesOutTheCardsThatLeaveAndClosesUpAsync()
    {
        var requests = 0;
        await using var stage = await GestureStage.StartAsync(step => Css + (step == 0
            ? Deck(Card("Oldest", 60, 60000, "<button type=\"submit\" name=\"_btn\" value=\"0|\" id=\"act\">Undo</button>")
                + Card("Middle", 50, 60000, "<button type=\"button\" data-aioros-closes id=\"close\">x</button>") + Card("Newest", 70, 50))
            : string.Empty), SwipeScript.Prefix);
        var page = stage.Page;
        page.Request += (_, request) => requests += request.Method == "POST" ? 1 : 0;
        await page.Mouse.MoveAsync(5, 5);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.notice > .nt').length === 2", null, new() { PollingInterval = 20 });
        var expired = await page.EvaluateAsync<string>(Stack);
        await page.EvaluateAsync("() => document.getElementById('close').click()");
        var closing = await page.EvaluateAsync<string>("() => document.getElementById('close').closest('.nt').hasAttribute('data-gone') + ' ' + document.getElementById('close').closest('.nt').getAnimations().find(a => a.animationName)?.playState");
        await page.WaitForFunctionAsync("() => !document.getElementById('close')", null, new() { PollingInterval = 20 });
        var closed = await page.EvaluateAsync<string>(Stack);
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.EvaluateAsync("() => document.getElementById('act').click()");
        await answered;
        await page.WaitForFunctionAsync("() => !document.querySelector('.notice')", null, new() { PollingInterval = 20 });

        Assert.Equal("50px 0px -14px|0.95|50px|b|running / 0px|1|auto|f|running", expired);
        Assert.Equal("true running", closing);
        Assert.Equal("60px 0px|1|auto|f|running", closed);
        Assert.Equal(1, requests);
        Assert.Equal(1, stage.Step);
    }

    [Fact]
    [Trait("AC", "SSR-247")]
    public async Task ALaterAnswersCardsJoinTheDeckInFrontAsync()
    {
        await using var stage = await GestureStage.StartAsync(step => Css + "<button type=\"submit\" name=\"_btn\" value=\"9|\" id=\"next\">Next</button>" + step switch
        {
            0 => Deck(Card("First", 60, 60000, "<button type=\"submit\" name=\"_btn\" value=\"1|\" id=\"a\">A</button>"), " data-replay=\"step=a\""),
            1 => Deck(Card("Second", 50, 60000, "<button type=\"submit\" name=\"_btn\" value=\"1|\" id=\"b\">B</button>"), " data-replay=\"step=b\""),
            _ => string.Empty,
        }, SwipeScript.Prefix);
        var page = stage.Page;
        await page.Mouse.MoveAsync(5, 5);
        await page.EvaluateAsync("() => { window.first = document.querySelector('.notice .nt'); window.time = window.first.getAnimations().find(a => a.animationName).currentTime }");
        var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#next");
        await answered;
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.notice .nt').length === 2", null, new() { PollingInterval = 20 });
        var joined = await page.EvaluateAsync<string>("() => { const g = document.querySelectorAll('.notice'), c = [...g[0].querySelectorAll('.nt')]; return g.length + ' ' + (c[0] === window.first) + ' ' + (c[0].getAnimations().find(a => a.animationName).currentTime > window.time) + ' ' + (c[1].dataset.replay ?? '-') }");
        var stack = await page.EvaluateAsync<string>(Stack);
        answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#next");
        await answered;
        await page.WaitForFunctionAsync("() => !document.querySelector('form[aria-busy]')", null, new() { PollingInterval = 20 });
        var kept = await page.EvaluateAsync<int>("() => document.querySelectorAll('.notice .nt').length");
        string? posted = null;
        page.Request += (_, request) => posted ??= request.Method == "POST" ? request.PostData : null;
        await page.HoverAsync("#b");
        answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
        await page.ClickAsync("#b");
        await answered;

        Assert.Equal("1 true true step=b", joined);
        Assert.Equal("50px 0px -14px|0.95|50px|b|running / 0px|1|auto|f|running", stack);
        Assert.Equal(2, kept);
        Assert.Contains("_prompt=step%3Db", posted, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-250")]
    public async Task ADecksCardsKeepTheSheetTheyCameWithAsync()
    {
        const string Sheets = "() => [...document.head.querySelectorAll('style[data-aioros-style]')].map(s => s.dataset.aiorosStyle + (s.hasAttribute('data-aioros-kept') ? '*' : '')).join(',')";
        await using var stage = await GestureStage.StartAsync(step => Css + "<button type=\"submit\" name=\"_btn\" value=\"9|\" id=\"next\">Next</button><div data-aioros-swipe=\"down\" data-aioros-swipe-by=\"45 0.11\"></div>" + step switch
        {
            0 => string.Empty,
            1 => "<style data-aioros-style=\"g:1\">.k1{outline:3px solid red}</style>" + Deck(Card("Kept", 60, 60000, "<b class=\"k1\">look</b><button type=\"button\" data-aioros-closes id=\"close\">Close</button>")),
            _ => "<style data-aioros-style=\"g:2\">.k2{outline:1px solid blue}</style>",
        }, SwipeScript.Prefix);
        var page = stage.Page;
        await page.Mouse.MoveAsync(5, 5);
        for (var post = 1; post <= 2; post++)
        {
            var answered = page.WaitForResponseAsync(response => response.Request.Method == "POST");
            await page.ClickAsync("#next");
            await answered;
            await page.WaitForFunctionAsync("() => !document.querySelector('form[aria-busy]') && document.querySelector('.notice .nt')", null, new() { PollingInterval = 20 });
        }

        await page.WaitForFunctionAsync("() => document.head.querySelector('style[data-aioros-style=\"g:2\"]')", null, new() { PollingInterval = 20 });
        var kept = await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.notice .k1')).outlineWidth") + " " + await page.EvaluateAsync<string>(Sheets);
        await page.ClickAsync("#close");
        await page.WaitForFunctionAsync("() => !document.querySelector('.notice')", null, new() { PollingInterval = 20 });
        var left = await page.EvaluateAsync<string>(Sheets);

        Assert.Equal("3px g:1*,g:2", kept);
        Assert.Equal("g:2", left);
    }

    [Fact]
    [Trait("AC", "SSR-248")]
    public async Task ACardSwipedPastItsDistanceLeavesTheWayItWentAsync()
    {
        const string Moved = "() => document.querySelector('.notice .nt[data-aioros-swipe-host]')?.style.transform ?? 'gone'";
        static string Swiped(string by) => Css + Deck(Card("Swiped", 70, 60000, "<button type=\"button\" id=\"in\">In</button>", " data-aioros-swipe-host").Replace("<div class=\"card\" style=\"position:relative\">", $"<div class=\"card\" style=\"position:relative\" data-aioros-swipe=\"down right\" data-aioros-swipe-by=\"{by}\">", StringComparison.Ordinal));
        await using var far = await GestureStage.StartAsync(_ => Swiped("45 1000"), SwipeScript.Prefix);
        var page = far.Page;
        var box = (await page.Locator(".notice .card").BoundingBoxAsync())!;
        var (x, y) = (box.X + 40, box.Y + 20);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 60, y, new() { Steps = 6 });
        var following = await page.EvaluateAsync<string>(Moved);
        await page.Mouse.UpAsync();
        var out1 = await page.EvaluateAsync<string>("() => document.querySelector('.notice .nt')?.dataset.out ?? 'gone'");
        await page.WaitForFunctionAsync("() => !document.querySelector('.notice')", null, new() { PollingInterval = 20 });
        await using var near = await GestureStage.StartAsync(_ => Swiped("45 1000"), SwipeScript.Prefix);
        page = near.Page;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 20, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        var back = await page.EvaluateAsync<string>(Moved);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 30, y, new() { Steps = 6 });
        var dampened = await page.EvaluateAsync<string>(Moved);
        await page.Mouse.UpAsync();
        var undamped = await page.EvaluateAsync<string>(Moved);
        var button = (await page.Locator("#in").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(button.X + 5, button.Y + 5);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(button.X + 80, button.Y + 5, new() { Steps = 6 });
        var fromButton = await page.EvaluateAsync<string>(Moved);
        await page.Mouse.UpAsync();
        await using var fast = await GestureStage.StartAsync(_ => Swiped("1000 0.001"), SwipeScript.Prefix);
        page = fast.Page;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 30, y, new() { Steps = 3 });
        await page.Mouse.UpAsync();
        var flicked = await page.EvaluateAsync<string>("() => document.querySelector('.notice .nt')?.dataset.out ?? 'gone'");

        Assert.Equal("translate(60px, 0px)", following);
        Assert.Equal("right", out1);
        Assert.Equal(string.Empty, back);
        Assert.Equal("translate(-10px, 0px)", dampened);
        Assert.Equal(string.Empty, undamped);
        Assert.Equal(string.Empty, fromButton);
        Assert.Contains(flicked, (string[])["right", "gone"]);
    }
}
