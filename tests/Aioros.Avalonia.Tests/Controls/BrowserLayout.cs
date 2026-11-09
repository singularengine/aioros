using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.Playwright;

namespace Aioros.Avalonia.Tests.Controls;

/// <summary>A compiled page in Chromium: its texts with the bounds of their lines, as the compatibility bench reads them, and its tab order.</summary>
internal static class BrowserLayout
{
    private const string Texts = """
        () => {
          const out = [], walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT)
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (!node.textContent.trim()) continue
            const range = document.createRange()
            range.selectNodeContents(node)
            const rects = [...range.getClientRects()].filter(rect => rect.width > 0 && rect.height > 0)
            if (!rects.length) continue
            const left = Math.min(...rects.map(r => r.left)), top = Math.min(...rects.map(r => r.top))
            out.push([node.textContent.trim(), left, top, Math.max(...rects.map(r => r.right)) - left, Math.max(...rects.map(r => r.bottom)) - top].join('\t'))
          }
          return out.join('\n')
        }
        """;

    public static Task<ImmutableArray<LaidOutText>> TextsAsync(CompiledPage page, int width, int height) => TextsAsync(page, EngineFixture.Body(page), width, height);

    /// <summary>The texts of a body the page's plan wrote for a model, laid out with the page's styles.</summary>
    public static async Task<ImmutableArray<LaidOutText>> TextsAsync(CompiledPage page, string body, int width, int height)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var tab = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        await tab.SetContentAsync(Document(page, body));
        var texts = await tab.EvaluateAsync<string>(Texts);
        return [.. texts.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split('\t')).Select(cells => new LaidOutText(cells[0], Number(cells[1]), Number(cells[2]), Number(cells[3]), Number(cells[4])))];
    }

    /// <summary>The text of each element Tab moves the focus to, starting from the element a selector finds.</summary>
    public static async Task<ImmutableArray<string>> TabsAsync(CompiledPage page, string start, int presses)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var tab = await browser.NewPageAsync();
        await tab.SetContentAsync(Document(page));
        await tab.FocusAsync(start);
        var focused = new List<string>();
        for (var press = 0; press < presses; press++)
        {
            await tab.Keyboard.PressAsync("Tab");
            focused.Add(await tab.EvaluateAsync<string>("() => document.activeElement === document.body ? 'body' : document.activeElement.textContent.trim()"));
        }

        return [.. focused];
    }

    /// <summary>What a script returns about the page laid out in Chromium at a viewport size.</summary>
    public static async Task<string> EvaluateAsync(CompiledPage page, int width, int height, string script)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var tab = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        await tab.SetContentAsync(Document(page));
        return await tab.EvaluateAsync<string>(script);
    }

    /// <summary>What a script returns about the page laid out in Chromium at a viewport size, once an element was clicked and a condition holds.</summary>
    public static async Task<string> ClickAndEvaluateAsync(CompiledPage page, int width, int height, string selector, string until, string script)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var tab = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        await tab.SetContentAsync(Document(page));
        await tab.ClickAsync(selector);
        await tab.WaitForFunctionAsync(until, null, new() { PollingInterval = 20, Timeout = 10000 });
        return await tab.EvaluateAsync<string>(script);
    }

    /// <summary>
    /// Runs steps on the page laid out in Chromium at a viewport size, with the browser's clock installed for the steps to
    /// move by hand and the given modules of the framework's script loaded into it.
    /// </summary>
    public static async Task<T> ClockedAsync<T>(CompiledPage page, int width, int height, IEnumerable<ReadOnlyMemory<byte>> modules, Func<IPage, Task<T>> steps)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        var tab = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        await tab.Clock.InstallAsync(new() { TimeDate = DateTime.UnixEpoch });
        await tab.Clock.PauseAtAsync(DateTime.UnixEpoch.AddMilliseconds(1024));
        await tab.SetContentAsync(Document(page));
        foreach (var module in modules)
        {
            await tab.AddScriptTagAsync(new() { Content = Encoding.UTF8.GetString(module.Span), Type = "module" });
        }

        return await steps(tab);
    }

    /// <summary>The page laid out in Chromium at a viewport size, as a PNG.</summary>
    public static async Task<byte[]> ScreenshotAsync(CompiledPage page, int width, int height)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox", "--force-color-profile=srgb"] });
        var tab = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        await tab.SetContentAsync(Document(page));
        return await tab.ScreenshotAsync();
    }

    /// <summary>The page as the web target writes it, its base styles and the page's own around its body.</summary>
    private static string Document(CompiledPage page) => Document(page, EngineFixture.Body(page));

    private static string Document(CompiledPage page, string body)
    {
        var parts = DocumentParts.For(page, new SsrOptions(), live: false);
        return "<!doctype html><html lang=\"en\">" + Encoding.UTF8.GetString(parts.Head) + "Layout" + Encoding.UTF8.GetString(parts.AfterTitle)
            + Encoding.UTF8.GetString(parts.Css) + Encoding.UTF8.GetString(parts.Start) + Encoding.UTF8.GetString(DocumentParts.BodyOpen)
            + body + Encoding.UTF8.GetString(parts.Tail);
    }

    /// <summary>Whether an element is shown, and its box, in a post's answer laid out in Chromium with JavaScript on or off.</summary>
    public static async Task<(bool Visible, Microsoft.Playwright.LocatorBoundingBoxResult? Box)> ShownAsync(CompiledPage page, string body, bool javaScript, string selector)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("AIOROS_CHROMIUM") ?? "/usr/bin/chromium", Args = ["--no-sandbox"] });
        await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = javaScript, ViewportSize = new() { Width = 800, Height = 600 } });
        var tab = await context.NewPageAsync();
        await tab.SetContentAsync(Document(page, body));
        return (await tab.IsVisibleAsync(selector), await tab.Locator(selector).BoundingBoxAsync());
    }

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);
}
