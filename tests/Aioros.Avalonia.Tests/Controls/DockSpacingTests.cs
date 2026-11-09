using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class DockSpacingTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "LAYOUT-95")]
    public async Task ADockPanelWithoutAFillingChildHasNoSpacingAfterItsLastAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel HorizontalAlignment="Left">
              <DockPanel a:Web.Id="sized" VerticalSpacing="8" LastChildFill="False" Width="100" VerticalAlignment="Top">
                <Border DockPanel.Dock="Top" Height="20" Background="Red" />
                <Border DockPanel.Dock="Top" Height="20" Background="Red" />
                <Border a:Web.Id="sizedEnd" DockPanel.Dock="Bottom" Height="20" Background="Red" />
              </DockPanel>
              <DockPanel a:Web.Id="tall" VerticalSpacing="8" LastChildFill="False" Width="100" Height="200">
                <Border DockPanel.Dock="Top" Height="20" Background="Red" />
                <Border a:Web.Id="tallSecond" DockPanel.Dock="Top" Height="20" Background="Red" />
                <Border a:Web.Id="tallEnd" DockPanel.Dock="Bottom" Height="20" Background="Red" />
              </DockPanel>
              <DockPanel a:Web.Id="row" HorizontalSpacing="8" LastChildFill="False" Height="20" HorizontalAlignment="Left">
                <Border DockPanel.Dock="Left" Width="20" Background="Red" />
                <Border DockPanel.Dock="Left" Width="20" Background="Red" />
                <Border DockPanel.Dock="Right" Width="20" Background="Red" />
              </DockPanel>
            </StackPanel>
            """);

        var boxes = await BrowserLayout.EvaluateAsync(page, 800, 600, """
            () => {
              const box = (id) => document.getElementById(id).getBoundingClientRect()
              const at = (id, panel) => Math.round(box(id).y - box(panel).y)
              return [Math.round(box('sized').height), at('sizedEnd', 'sized'), at('tallSecond', 'tall'), at('tallEnd', 'tall'), Math.round(box('row').width)].join()
            }
            """);

        Assert.Equal("76,56,28,180,76", boxes);
    }
}
