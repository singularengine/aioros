using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class HierarchyPlanTests(EngineFixture fixture)
{
    private const string Menu = """
        <StackPanel>
          <StackPanel.Resources>
            <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/OutlineTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
          </StackPanel.Resources>
          <m:Outline ItemsSource="{Binding Nodes}" SubItemsBinding="{Binding Children}" HeaderBinding="{Binding}" IconBinding="{Binding Badge^}">
            <m:Outline.Styles>
              <Style x:DataType="m:Node" Selector="m|OutlineItem">
                <Setter Property="IsSeparator" Value="{Binding IsSeparator}" />
                <Setter Property="IsVisible" Value="{Binding Shown, Mode=TwoWay}" />
              </Style>
            </m:Outline.Styles>
            <m:Outline.IconTemplate>
              <DataTemplate x:DataType="x:String">
                <StackPanel Orientation="Horizontal">
                  <PathIcon Width="12" Height="12" Data="{Binding Converter={x:Static m:NameIcons.Instance}}" />
                  <m:Picked IsOn="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=m:OutlineItem}, Path=IsPicked, Mode=TwoWay}" />
                </StackPanel>
              </DataTemplate>
            </m:Outline.IconTemplate>
            <m:Outline.HeaderTemplate>
              <DataTemplate x:DataType="m:Node"><StackPanel Orientation="Horizontal"><TextBlock Text="{Binding Name}" /></StackPanel></DataTemplate>
            </m:Outline.HeaderTemplate>
          </m:Outline>
        </StackPanel>
        """;

    private static Outlined Compiled { get; } = new(new Node("Home"), new Node("Controls", IsSeparator: true), new Node("Inputs", Children: [new Node("Button"), new Node("Slider")]));

    [Fact]
    [Trait("AC", "ITEMS-80")]
    public async Task MenusOfMenusAreLearnedPerLevelAndLookAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            Menu,
            Compiled,
            new Outlined(new Node("Hidden", Visible: false), new Node("Shelf", Children: [new Node("Inner", IsSeparator: true), new Node("Box")]), new Node("Index")),
            new Outlined());

        Assert.Contains(">Slider<", EngineFixture.Body(planned, Compiled), StringComparison.Ordinal);
        Assert.All(Compiled.Nodes, node => Assert.True(node.Shown));
    }
}
