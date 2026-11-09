using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Motion;

public sealed class RevealTests(DesktopFixture fixture)
{
    private const string Source = "Pages/reveals/Page.axaml";

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <UserControl.Styles>
            <Style Selector="Border.enter">
              <Style.Animations>
                <Animation Duration="0:0:0.05" FillMode="Both">
                  <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame>
                  <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                </Animation>
              </Style.Animations>
            </Style>
          </UserControl.Styles>
          <ScrollViewer Height="120">
            <StackPanel>
              <Border Name="Top" Classes="enter" a:Web.Reveal="True" Height="40" />
              <Border Height="2000" />
              <Border Name="Below" Classes="enter" a:Web.Reveal="True" Height="40" />
            </StackPanel>
          </ScrollViewer>
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-100")]
    public async Task RevealedControlsPlayAgainWhenTheyFirstScrollIntoViewAsync()
    {
        var page = DesktopFixture.Page("/reveals", Source, _ => new Reloaded());
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml);

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/reveals");
            DesktopFixture.Settle(window);
            Frames();
            var opened = (Named(window, "Top").Opacity, Named(window, "Below").Opacity);
            var viewer = window.GetVisualDescendants().OfType<ScrollViewer>().First(control => control.Height is > 119 and < 121);

            viewer.Offset = new Vector(0, 2000);
            DesktopFixture.Settle(window);
            var entered = (Named(window, "Top").Opacity, Named(window, "Below").Opacity);
            Frames();
            var played = Named(window, "Below").Opacity;
            viewer.Offset = default;
            DesktopFixture.Settle(window);
            viewer.Offset = new Vector(0, 2000);
            DesktopFixture.Settle(window);
            var again = Named(window, "Below").Opacity;
            window.Close();
            return (opened, entered, played, again);
        });

        Assert.True(result.opened is ( > 0, > 0), $"{result.opened}");
        Assert.Equal(0, result.entered.Item2);
        Assert.True(result.entered.Item1 >= result.opened.Item1, $"{result.entered} after {result.opened}");
        Assert.True(result.played > 0 && result.again >= result.played, $"{result.played}, {result.again}");
    }

    private static Border Named(Window window, string name) => window.GetVisualDescendants().OfType<Border>().First(control => control.Name == name);

    private static void Frames()
    {
        for (var frame = 0; frame < 12; frame++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
