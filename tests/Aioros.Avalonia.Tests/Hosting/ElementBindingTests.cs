using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class ElementBindingTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-96")]
    public async Task UnboundElementPropertiesAreWrittenAsRenderedAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Styles><Style Selector="TextBlock.loud"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles>
              <Slider x:Name="slider" Minimum="0" Maximum="100" Value="40" />
              <ProgressBar Value="{Binding #slider.Value}" />
              <TextBlock Text="{Binding Value, ElementName=slider}" />
              <Label Content="{Binding #slider.Value, StringFormat='Value: {0}'}" />
              <CheckBox x:Name="active" IsChecked="True">Active</CheckBox>
              <TextBlock Text="classed" Classes.loud="{Binding #active.IsChecked}" />
              <TextBlock Text="hidden" IsVisible="{Binding !#active.IsChecked}" />
              <TextBlock Text="{Binding Text}" />
              <ToggleButton x:Name="toggle" IsChecked="True">Pane</ToggleButton>
              <SplitView x:Name="split" IsPaneOpen="{Binding #toggle.IsChecked, Mode=TwoWay}" />
              <TextBlock Text="open" IsVisible="{Binding #split.IsPaneOpen}" />
              <ProgressBar Minimum="{Binding #slider.Minimum}" Maximum="{Binding #slider.Maximum}" Value="5" />
              <Button IsEnabled="{Binding !#active.IsChecked}">Off</Button>
              <ComboBox x:Name="direction" SelectedIndex="1">
                <ExpandDirection>Down</ExpandDirection>
                <ExpandDirection>Up</ExpandDirection>
              </ComboBox>
              <Expander Header="More" ExpandDirection="{Binding #direction.SelectedItem}" />
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="x:String">
                    <TextBlock Text="{Binding}" IsVisible="{Binding $parent[ItemsControl].IsEnabled}" />
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <ItemsControl ItemsSource="{Binding Items}" IsEnabled="{Binding #active.IsChecked}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="x:String">
                    <TextBlock Text="{Binding}" IsVisible="{Binding !$parent[ItemsControl].IsEnabled}" />
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, Tile.On);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in new[] { Tile.On, Tile.Off })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
            Assert.Contains($">{model.Text}<", expected, StringComparison.Ordinal);
            Assert.Contains(">40<", expected, StringComparison.Ordinal);
            Assert.Contains(">Value: 40<", expected, StringComparison.Ordinal);
            Assert.Contains(">classed<", expected, StringComparison.Ordinal);
            Assert.DoesNotContain(">hidden<", expected, StringComparison.Ordinal);
            Assert.Contains(">open<", expected, StringComparison.Ordinal);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(expected, $">{model.Items[0]}<"));
        }

        var bound = await fixture.CompileAsync("""<StackPanel><ComboBox x:Name="pick" ItemsSource="{Binding Items}" SelectedIndex="0" /><TextBlock Text="{Binding #pick.SelectedItem}" /></StackPanel>""", Tile.On);
        Assert.False(bound.Reusable);
        Assert.Contains("#pick.SelectedItem", bound.Failure, StringComparison.Ordinal);
        Assert.Contains("ComboBox.ItemsSource", bound.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-97")]
    public async Task BoundContentIsWrittenAsItsTextAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="m|Captioned">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel>
                        <ContentPresenter Name="PART_HeaderPresenter" Content="{TemplateBinding Header}" FontWeight="Bold" />
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </Style>
              </StackPanel.Styles>
              <ContentControl Content="{Binding Size}" />
              <m:Captioned Header="Created" Content="{Binding Created}" />
              <m:Captioned Header="Done" Content="{Binding Done, TargetNullValue='Not yet completed'}" />
            </StackPanel>
            """;
        var open = new Dated(2.5, new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Unspecified), null);
        var done = new Dated(7, new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified));
        var planned = await fixture.CompileAsync(Xaml, open);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in new[] { open, done })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }

        Assert.Contains(">Not yet completed<", EngineFixture.Body(saved, open), StringComparison.Ordinal);
        Assert.Contains(">7<", EngineFixture.Body(saved, done), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-98")]
    public async Task TemplateMultiBindingsOfConstantsAreWrittenAsRenderedAsync()
    {
        const string Styles = """
            <StackPanel.Styles>
              <Style Selector="HeaderedContentControl">
                <Setter Property="Template">
                  <ControlTemplate>
                    <StackPanel>
                      <ContentPresenter Name="PART_HeaderPresenter" Content="{TemplateBinding Header}" />
                      <TextBlock Text="headed" IsVisible="{Binding Header, RelativeSource={RelativeSource TemplatedParent}, Converter={x:Static ObjectConverters.IsNotNull}}" />
                      <Border Height="1" Background="Red">
                        <Border.IsVisible>
                          <MultiBinding Converter="{x:Static m:BothSet.Instance}">
                            <TemplateBinding Property="Header" />
                            <TemplateBinding Property="Content" />
                          </MultiBinding>
                        </Border.IsVisible>
                      </Border>
                      <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                    </StackPanel>
                  </ControlTemplate>
                </Setter>
              </Style>
            </StackPanel.Styles>
            """;
        var xaml = "<StackPanel>" + Styles + """
            <HeaderedContentControl Header="Title"><TextBlock Text="Body" /></HeaderedContentControl>
            <HeaderedContentControl Header="Alone" />
            <m:Sections><HeaderedContentControl Header="Item"><TextBlock Text="Inside" /></HeaderedContentControl></m:Sections>
            <TextBlock Text="{Binding Text}" />
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(xaml, Tile.On);

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in new[] { Tile.On, Tile.Off })
        {
            Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(xaml, model), model), EngineFixture.Body(planned, model));
        }

        var bound = await fixture.CompileAsync("<StackPanel>" + Styles + """<HeaderedContentControl Header="Title" Content="{Binding Text}" /></StackPanel>""", Tile.On);
        Assert.False(bound.Reusable);
        Assert.Contains("MultiBinding", bound.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-329")]
    public async Task ABoundTrackFollowsThePartItSpacesAsync()
    {
        const string Body = """
            <m:SpacedRows Width="300">
              <TextBlock Text="Email" />
              <TextBox Text="{Binding Name}" Width="200" AutomationProperties.Name="Email" />
              <TextBlock Text="We send one email a month." IsVisible="{Binding Agree}" />
              <TextBlock Text="Name" />
            </m:SpacedRows>
            """;

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Profile { Agree = true }, new Profile { Agree = false });
        var css = planned.Css;
        var hidden = EngineFixture.Body(planned, new Profile { Agree = false });

        Assert.Contains("var(--r3", css, StringComparison.Ordinal);
        Assert.Contains("--r3:0px", hidden, StringComparison.Ordinal);
        Assert.Contains("--r5:12px", hidden, StringComparison.Ordinal);
    }

}
