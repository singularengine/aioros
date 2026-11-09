using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ScrollLayoutTests(EngineFixture fixture)
{
    private const string Strip = """
        <StackPanel Orientation="Horizontal" Spacing="16">
          <Border Width="300" Height="40" Background="Red" />
          <Border Width="300" Height="40" Background="Green" />
          <Border Width="300" Height="40" Background="Blue" />
        </StackPanel>
        """;

    private const string Scrolled = """
        () => ["fluent", "framed", "mixed"].map((id) => {
          const viewer = document.getElementById(id), style = getComputedStyle(viewer)
          viewer.scrollLeft = 100000
          return [id, style.overflowX, style.scrollbarWidth, viewer.scrollWidth, Math.round(viewer.scrollLeft) === viewer.scrollWidth - viewer.clientWidth && viewer.scrollLeft > 0].join(" ")
        }).join("\n")
        """;

    [Fact]
    [Trait("AC", "LAYOUT-80")]
    public async Task AScrollViewerWithAHiddenBarScrollsWithoutItAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel Spacing="16" HorizontalAlignment="Left">
              <StackPanel.Resources>
                <ControlTheme x:Key="Framed" TargetType="ScrollViewer">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="{TemplateBinding Background}" ClipToBounds="True">
                        <ScrollContentPresenter Name="PART_ContentPresenter" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <ScrollViewer a:Web.Id="fluent" Width="384" HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Disabled">{{Strip}}</ScrollViewer>
              <ScrollViewer a:Web.Id="framed" Theme="{StaticResource Framed}" Width="384" HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Disabled">{{Strip}}</ScrollViewer>
              <ScrollViewer a:Web.Id="mixed" Width="384" Height="80" HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Auto">{{Strip}}</ScrollViewer>
            </StackPanel>
            """);

        var found = (await BrowserLayout.EvaluateAsync(page, 800, 400, Scrolled)).Split('\n');

        Assert.Equal(["fluent auto none 932 true", "framed auto none 932 true", "mixed auto auto 932 true"], found);
    }
}
