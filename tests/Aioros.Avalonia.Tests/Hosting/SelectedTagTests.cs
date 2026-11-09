using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class SelectedTagTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-39")]
    public async Task SelectorsOnABoundTagRenderEachValueAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="Border[Tag=on]"><Setter Property="Background" Value="Red" /></Style>
                <Style Selector="Border[Tag=off] TextBlock"><Setter Property="FontWeight" Value="Bold" /></Style>
                <Style Selector="TextBlock"><Style Selector="^[Tag=1]"><Setter Property="FontStyle" Value="Italic" /></Style></Style>
                <Style Selector="TextBlock[Tag=01]"><Setter Property="Foreground" Value="Blue" /></Style>
              </StackPanel.Styles>
              <Border Height="4" Tag="{Binding Mode}"><TextBlock Text="mode" /></Border>
              <TextBlock Text="col" Tag="{Binding Col}" />
              <Button Content="go" Tag="{Binding Mode}">
                <Button.Theme>
                  <ControlTheme TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                    <Style Selector="^[Tag=off]"><Setter Property="Foreground" Value="Green" /></Style>
                  </ControlTheme>
                </Button.Theme>
              </Button>
            </StackPanel>
            """;
        var models = new[] { Tile.On, Tile.Off, new Tile(true, "none", 0, [], "#000000", "ON", new Circle(1), 0, 9, "Left") };
        var planned = await fixture.CompileAsync(Xaml, Tile.On);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        Assert.NotEqual(EngineFixture.Body(planned, Tile.On), EngineFixture.Body(planned, Tile.Off));
        foreach (var model in models)
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }

        foreach (var (tag, selector, reason) in new[]
        {
            ("{Binding Child}", "on", "sealed"),
            ("{Binding Mode, Converter={x:Static StringConverters.IsNotNullOrEmpty}}", "True", "converter"),
            ("{Binding Col, StringFormat='n{0}'}", "n1", "StringFormat"),
        })
        {
            var page = await fixture.CompileAsync($$"""<StackPanel><StackPanel.Styles><Style Selector="TextBlock[Tag={{selector}}]"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles><TextBlock Text="x" Tag="{{tag}}" /></StackPanel>""", Tile.On);
            Assert.False(page.Reusable, tag);
            Assert.Contains("TextBlock.Tag", page.Failure, StringComparison.Ordinal);
            Assert.Contains(reason, page.Failure, StringComparison.Ordinal);
        }
    }
}
