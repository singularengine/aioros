using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Motion;

public sealed class ParallaxTests(DesktopFixture fixture)
{
    private const string Source = "Pages/parallax/Page.axaml";

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <ScrollViewer Height="600">
            <StackPanel>
              <Border Height="450" />
              <Border Name="Layer" Height="100" Background="Red" a:Web.Parallax="0.5" />
              <Border Height="2000" />
            </StackPanel>
          </ScrollViewer>
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-105")]
    public async Task LayersShiftAgainstTheScrollByTheirShareAsync()
    {
        var page = DesktopFixture.Page("/parallax", Source, _ => new Reloaded());
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml);

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/parallax");
            DesktopFixture.Settle(window);
            var layer = window.GetVisualDescendants().OfType<Border>().First(control => control.Name == "Layer");
            var viewer = window.GetVisualDescendants().OfType<ScrollViewer>().First(control => control.Height is > 599 and < 601);
            viewer.Offset = new Vector(0, 1);
            DesktopFixture.Settle(window);
            var opened = (layer.RenderTransform as TranslateTransform)?.Y;
            viewer.Offset = new Vector(0, 200);
            DesktopFixture.Settle(window);
            var middle = (layer.RenderTransform as TranslateTransform)?.Y;
            window.Close();
            return (opened, middle);
        });

        Assert.Equal(((double?)-100, (double?)0), result);
    }
}
