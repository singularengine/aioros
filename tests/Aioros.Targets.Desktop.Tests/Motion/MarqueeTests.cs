using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Motion;

public sealed class MarqueeTests(DesktopFixture fixture)
{
    private const string Source = "Pages/marquees/Page.axaml";

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <Border ClipToBounds="True">
            <StackPanel Name="Band" Orientation="Horizontal" HorizontalAlignment="Left" Spacing="8" a:Web.Marquee="2">
              <Border Width="100" Height="20" Background="Red" /><Border Width="100" Height="20" Background="Red" /><Border Width="100" Height="20" Background="Red" /><Border Width="100" Height="20" Background="Red" />
            </StackPanel>
          </Border>
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-103")]
    public async Task MarqueesMoveLeftOnEachFrameAsync()
    {
        var page = DesktopFixture.Page("/marquees", Source, _ => new Reloaded());
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml);

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/marquees");
            DesktopFixture.Settle(window);
            var band = window.GetVisualDescendants().OfType<StackPanel>().First(control => control.Name == "Band");
            Frame();
            var first = Offset(band);
            for (var frame = 0; frame < 6; frame++)
            {
                Frame();
            }

            var later = Offset(band);
            var width = band.Bounds.Width;
            window.Close();
            return (first, later, width);
        });

        Assert.True(result.later < result.first, $"{result.first} -> {result.later}");
        Assert.True(result.later >= -result.width / 2, $"{result.later} of {result.width}");
    }

    private static double Offset(Control control) => control.RenderTransform is TranslateTransform move ? move.X : double.NaN;

    private static void Frame()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
