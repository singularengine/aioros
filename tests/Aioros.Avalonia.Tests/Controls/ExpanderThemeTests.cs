using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ExpanderThemeTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "NAVIGATION-96")]
    public async Task AnExpandersContentKeepsItsPresentersBoxAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Trigger" TargetType="ToggleButton">
                  <Setter Property="Template">
                    <ControlTemplate><Border Name="PART_Root" Padding="0,2"><ContentPresenter Content="{TemplateBinding Content}" /></Border></ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Trace" TargetType="Expander">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel>
                        <ToggleButton Name="ExpanderHeader" Theme="{StaticResource Trigger}" HorizontalAlignment="Left" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}" Content="{TemplateBinding Header}" />
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" Margin="7,8,0,4" Padding="16,0,4,0" BorderThickness="1,0,0,0" BorderBrush="#e5e5e5" IsVisible="{TemplateBinding IsExpanded}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Plain" TargetType="Expander">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel Spacing="8">
                        <ToggleButton Name="ExpanderHeader" Theme="{StaticResource Trigger}" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}" Content="{TemplateBinding Header}" />
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" IsVisible="{TemplateBinding IsExpanded}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Expander Theme="{StaticResource Trace}" Width="400" IsExpanded="True" Header="Searched the web"><TextBlock Text="Found three sources" /></Expander>
              <Expander Theme="{StaticResource Plain}" Width="400" IsExpanded="True" Header="Read the docs"><TextBlock Text="Two pages" /></Expander>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rules(string pattern) => string.Join('\n', Regex.Match(html, pattern).Groups["classes"].Value.Split(' ').Select(name => Regex.Match(page.Css, $"\\.{name}\\{{(?<css>[^}}]*)\\}}").Groups["css"].Value));
        var traceSummary = Rules("<summary class=\"(?<classes>[^\"]+)\">(<[^>]+>)*Searched the web");
        var traceContent = Rules("</summary><div class=\"(?<classes>[^\"]+)\">(<[^>]+>)*Found three sources");
        var plainSummary = Rules("<summary class=\"(?<classes>[^\"]+)\">(<[^>]+>)*Read the docs");
        var plainContent = Rules("</summary><div class=\"(?<classes>[^\"]+)\">(<[^>]+>)*Two pages");

        Assert.Contains("width:fit-content;", traceSummary, StringComparison.Ordinal);
        Assert.Contains("margin:8px 0px 4px 7px;", traceContent, StringComparison.Ordinal);
        Assert.Contains("padding:0px 4px 0px 16px;", traceContent, StringComparison.Ordinal);
        Assert.Contains("border-color:#e5e5e5;border-width:0px 0px 0px 1px;border-style:solid;", traceContent, StringComparison.Ordinal);
        Assert.DoesNotContain("fit-content", plainSummary, StringComparison.Ordinal);
        Assert.Contains("margin-top:8px;", plainContent, StringComparison.Ordinal);
        Assert.DoesNotContain("border", plainContent, StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "NAVIGATION-103")]
    public async Task AnExpandersTriggerKeepsItsOwnColorAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Trace" TargetType="Expander">
                  <Setter Property="Foreground" Value="#737373" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel>
                        <ToggleButton Name="ExpanderHeader" Foreground="#737373" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}" Content="{TemplateBinding Header}">
                          <ToggleButton.Template><ControlTemplate><Border Name="PART_Root"><ContentPresenter Content="{TemplateBinding Content}" /></Border></ControlTemplate></ToggleButton.Template>
                        </ToggleButton>
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" IsVisible="{TemplateBinding IsExpanded}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^.failed /template/ ToggleButton#ExpanderHeader"><Setter Property="Foreground" Value="#e7000b" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <Expander Theme="{StaticResource Trace}" Classes="failed" IsExpanded="True" Header="Ran 2 commands, 1 failed"><TextBlock Text="Output" /></Expander>
              <Expander Theme="{StaticResource Trace}" IsExpanded="True" Header="Ran node"><TextBlock Text="Plain" /></Expander>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rules(string pattern) => string.Join('\n', Regex.Match(html, pattern).Groups["classes"].Value.Split(' ').Select(name => Regex.Match(page.Css, $"\\.{name}\\{{(?<css>[^}}]*)\\}}").Groups["css"].Value));

        Assert.Contains("color:#e7000b;", Rules("<summary class=\"(?<classes>[^\"]+)\">(<[^>]+>)*Ran 2 commands"), StringComparison.Ordinal);
        Assert.DoesNotContain("color:", Rules("<summary class=\"(?<classes>[^\"]+)\">(<[^>]+>)*Ran node"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-104")]
    public async Task AChevronTurnedThroughATransitionTurnsWithItsDetailsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Label" TargetType="ToggleButton">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Panel>
                        <DockPanel>
                          <Viewbox Name="PART_Chevron" DockPanel.Dock="Right" Width="16" Height="16">
                            <Viewbox.Transitions><Transitions><TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.2" /></Transitions></Viewbox.Transitions>
                            <Path Data="M9,18 L15,12 L9,6" Stroke="#0a0a0a" StrokeThickness="2" />
                          </Viewbox>
                          <ContentPresenter Content="{TemplateBinding Content}" />
                        </DockPanel>
                      </Panel>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:checked /template/ Viewbox#PART_Chevron"><Setter Property="RenderTransform" Value="rotate(90deg)" /></Style>
                </ControlTheme>
                <ControlTheme x:Key="Group" TargetType="Expander">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel>
                        <ToggleButton Name="ExpanderHeader" Theme="{StaticResource Label}" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}" Content="{TemplateBinding Header}" />
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" IsVisible="{TemplateBinding IsExpanded}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Expander Theme="{StaticResource Group}" IsExpanded="True" Header="Getting Started"><TextBlock Text="Installation" /></Expander>
              <Expander Theme="{StaticResource Group}" Header="Community"><TextBlock Text="Contribution Guide" /></Expander>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        var turns = Regex.Matches(page.Css, @"details:not\(\[open\]\)>summary \.(?<name>xo[a-z0-9]+)\{transform:none\}details\[open\]>summary \.\k<name>\{transform:(?<open>[^}]+)\}").ToList();

        Assert.Single(turns);
        Assert.Matches(@"^matrix\(0(\.0+)?, ?1(\.0+)?, ?-1(\.0+)?, ?0(\.0+)?, ?0, ?0\)$|^rotate\(90deg\)$", turns[0].Groups["open"].Value);
        Assert.Equal(2, Regex.Count(html, $"class=\"[^\"]*\\b{turns[0].Groups["name"].Value}\\b"));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-105")]
    public async Task AToggleWithItsOwnHeightGivesItToItsSummaryAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Width="240" HorizontalAlignment="Left">
              <StackPanel.Resources>
                <ControlTheme x:Key="Label" TargetType="ToggleButton">
                  <Setter Property="Height" Value="32" />
                  <Setter Property="Padding" Value="8,0" />
                  <Setter Property="FontSize" Value="14" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                  <Setter Property="VerticalContentAlignment" Value="Center" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Panel>
                        <Border Name="PART_Border" Padding="{TemplateBinding Padding}">
                          <DockPanel>
                            <ContentPresenter Content="{TemplateBinding Content}" VerticalContentAlignment="Center" TextBlock.LineHeight="20" />
                          </DockPanel>
                        </Border>
                      </Panel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Group" TargetType="Expander">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel>
                        <ToggleButton Name="ExpanderHeader" Theme="{StaticResource Label}" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}" Content="{TemplateBinding Header}" />
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" IsVisible="{TemplateBinding IsExpanded}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Expander Theme="{StaticResource Group}" IsExpanded="True" Header="Getting Started"><TextBlock Text="Installation" /></Expander>
            </StackPanel>
            """);

        var laid = await BrowserLayout.EvaluateAsync(page, 800, 600, """
            () => {
              const summary = document.querySelector('summary').getBoundingClientRect()
              const line = (text) => { const node = [...document.querySelectorAll('div')].find((div) => div.childElementCount === 0 && div.textContent === text); const range = document.createRange(); range.selectNodeContents(node); return range.getBoundingClientRect() }
              const label = line('Getting Started'), content = line('Installation')
              return [Math.round(summary.height), Math.round(label.y + label.height / 2 - summary.y), Math.round(content.y - summary.y)].join()
            }
            """);

        Assert.Equal("32,16,32", laid);
    }
}
