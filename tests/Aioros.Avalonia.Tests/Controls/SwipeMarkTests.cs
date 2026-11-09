using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class SwipeMarkTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "OVERLAY-17")]
    public async Task SwipedElementsAndClosingButtonsAreMarkedAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Border a:Web.Swipe="Down, Right, Sideways" a:Web.Id="toast" Height="20" Background="Red" />
              <Border a:Web.Swipe="Up" a:Web.SwipeDistance="0.25" a:Web.SwipeVelocity="0.4" a:Web.Id="drawer" Height="20" Background="Blue" />
              <Border a:Web.Swipe="Sideways" a:Web.Id="none" Height="20" Background="Green" />
              <Button a:Web.Closes="True" Content="Close" />
            </StackPanel>
            """);

        Assert.Matches("<div data-aioros-swipe=\"down right\" data-aioros-swipe-by=\"45 0.11\" id=\"toast\"", html);
        Assert.Matches("<div data-aioros-swipe=\"up\" data-aioros-swipe-by=\"0.25 0.4\" id=\"drawer\"", html);
        Assert.Matches("<div id=\"none\"", html);
        Assert.Matches("<button type=\"button\" data-aioros-closes[^>]*>(?:<[^>]+>)*Close<", html);
        Assert.DoesNotContain("name=\"_btn\"", Regex.Match(html, "<button[^>]*data-aioros-closes[^>]*>").Value, StringComparison.Ordinal);
    }
}
