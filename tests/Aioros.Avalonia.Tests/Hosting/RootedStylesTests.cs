using Aioros.Avalonia.Hosting;
using Aioros.Runtime.Rendering;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class RootedStylesTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-158")]
    public async Task BoundBoxValuesOnControlsAreInlineStylesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <Button Width="{Binding Size2}" Opacity="{Binding Size}" Foreground="{Binding Brush}" FontSize="{Binding Size2}">Go</Button>
              <CheckBox Margin="{Binding Gap}" Height="{Binding Size2}">Check</CheckBox>
              <TextBox Opacity="{Binding Size}" Text="t" />
              <Slider Width="{Binding Size2}" Value="3" />
              <ProgressBar Margin="{Binding Gap}" Value="4" />
              <DatePicker Opacity="{Binding Size}" />
              <Button Content="r"><Button.RenderTransform><RotateTransform Angle="{Binding Size2}" /></Button.RenderTransform></Button>
            </StackPanel>
            """;
        foreach (var (from, to) in new[] { (Tile.On, Tile.Off), (Tile.Off, Tile.On) })
        {
            var planned = await fixture.CompileAsync(Xaml, from);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, to), to);

            Assert.True(planned.Reusable, planned.Failure);
            Assert.Equal(expected, EngineFixture.Body(planned, to));
            Assert.Equal(expected, EngineFixture.Body(saved, to));
            Assert.Equal(5, System.Text.RegularExpressions.Regex.Count(expected, $"opacity:{to.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)}[;\"]|margin:{to.Size2}px"));
        }

        var content = await fixture.CompileAsync("""<Button Foreground="{Binding Brush}"><TextBlock Text="Go" /></Button>""", Tile.On);
        Assert.False(content.Reusable);
        Assert.Contains("Button.Foreground", content.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-159")]
    public async Task BoundFramesOnControlsAreInlineStylesAsync()
    {
        const string Painted = """
            <StackPanel>
              <Button Background="{Binding Brush}">Go</Button>
              <ToggleButton Background="{Binding Brush}">T</ToggleButton>
              <TextBox Background="{Binding Brush}" Text="t" />
              <ComboBox Background="{Binding Brush}" />
            </StackPanel>
            """;
        const string Framed = """
            <StackPanel>
              <Button Padding="{Binding Pad}" CornerRadius="{Binding Radius}">Go</Button>
              <ToggleButton Padding="{Binding Pad}">T</ToggleButton>
              <TextBox Padding="{Binding Pad}" CornerRadius="{Binding Radius}" Text="t" />
              <ComboBox CornerRadius="{Binding Radius}" />
            </StackPanel>
            """;
        foreach (var (xaml, models) in new (string, object[])[] { (Painted, [Tile.On, Tile.Off]), (Framed, [Shaped.On, Shaped.Off]) })
        {
            foreach (var (from, to) in new[] { (models[0], models[1]), (models[1], models[0]) })
            {
                var planned = await fixture.CompileAsync(xaml, from);
                var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
                var expected = EngineFixture.Body(await fixture.CompileAsync(xaml, to), to);

                Assert.True(planned.Reusable, planned.Failure);
                Assert.Equal(expected, EngineFixture.Body(planned, to));
                Assert.Equal(expected, EngineFixture.Body(saved, to));
            }
        }

        Assert.Equal(4, System.Text.RegularExpressions.Regex.Count(EngineFixture.Body(await fixture.CompileAsync(Painted, Tile.On), Tile.Off), "style=\"background:#00ff00\""));
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(EngineFixture.Body(await fixture.CompileAsync(Framed, Shaped.On), Shaped.Off), "padding:9px 9px 9px 9px[;\"]"));

        var check = await fixture.CompileAsync("""<CheckBox Padding="{Binding Pad}">Go</CheckBox>""", Shaped.On);
        Assert.False(check.Reusable);
        Assert.Contains("CheckBox.Padding", check.Failure, StringComparison.Ordinal);
    }
}
