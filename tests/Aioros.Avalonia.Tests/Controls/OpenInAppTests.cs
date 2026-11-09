using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class OpenInAppTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "INSTALL-10")]
    public async Task OpenInAppLinksAreMarkedForTheScriptAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <HyperlinkButton NavigateUri="/download" Content="Open in the desktop app" a:Web.OpenInApp="True" />
              <StackPanel a:Web.Href="/download" a:Web.OpenInApp="True"><TextBlock Text="Get the app" /></StackPanel>
              <HyperlinkButton NavigateUri="/download" Content="Download" />
            </StackPanel>
            """);

        Assert.Matches("<a href=\"/download\" data-open-app class=\"lk [s0-9a-f ]*k[0-9a-f]+\">Open in the desktop app</a>", html);
        Assert.Matches("<a data-open-app href=\"/download\" data-nav class=\"[^\"]+\">.*Get the app.*</a>", html);
        Assert.Matches("<a href=\"/download\" class=\"lk [s0-9a-f ]*k[0-9a-f]+\">Download</a>", html);
        Assert.Equal(2, html.Split("data-open-app").Length - 1);
    }
}
