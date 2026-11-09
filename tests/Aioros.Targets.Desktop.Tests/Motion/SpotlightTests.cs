using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Motion;

public sealed class SpotlightTests(DesktopFixture fixture)
{
    private const string Source = "Pages/spotlights/Page.axaml";

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <StackPanel>
            <Border Name="Card" Width="300" Height="200" HorizontalAlignment="Left" Background="#111111" CornerRadius="12" a:Web.Spotlight="#33FFFFFF" />
            <Border Name="Plain" Width="300" Height="200" HorizontalAlignment="Left" Background="#222222" />
          </StackPanel>
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-104")]
    public async Task TheGlowFollowsThePointerAsync()
    {
        var page = DesktopFixture.Page("/spotlights", Source, _ => new Reloaded());
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml);

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/spotlights");
            DesktopFixture.Settle(window);
            var card = window.GetVisualDescendants().OfType<Border>().First(control => control.Name == "Card");
            var origin = card.TranslatePoint(default, window)!.Value;
            window.MouseMove(origin + new Point(40, 30), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var glow = AdornerLayer.GetAdorner(card) as Border;
            var center = glow?.Background is RadialGradientBrush brush ? brush.Center.Point : default;
            var takesInput = glow?.IsHitTestVisible;
            window.MouseMove(origin + new Point(40, 250), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var gone = AdornerLayer.GetAdorner(card) is null;
            window.Close();
            return (glow is not null, center, takesInput, gone);
        });

        Assert.Equal((true, new Point(40, 30), (bool?)false, true), result);
    }
}
