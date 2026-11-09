using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class TabTemplateTests(EngineFixture fixture)
{
    private static string Tabs(string placement, double spacing, bool line, bool content = true) => $$"""
        <Panel>
        <Panel.Styles>
          <Style Selector="TabControl[TabStripPlacement=Left] TabItem /template/ Border#PART_SelectedPipe">
            <Setter Property="Width" Value="2" />
            <Setter Property="Height" Value="NaN" />
            <Setter Property="HorizontalAlignment" Value="Right" />
            <Setter Property="VerticalAlignment" Value="Stretch" />
            <Setter Property="Margin" Value="0,0,-4,0" />
          </Style>
        </Panel.Styles>
        <TabControl TabStripPlacement="{{placement}}" SelectedIndex="0" Width="400" HorizontalAlignment="Left" VerticalAlignment="Top">
          <TabControl.Theme>
            <ControlTheme TargetType="TabControl">
              <Setter Property="ClipToBounds" Value="False" />
              <Setter Property="ItemsPanel"><ItemsPanelTemplate><StackPanel Orientation="Horizontal" Spacing="{{spacing}}" /></ItemsPanelTemplate></Setter>
              <Setter Property="Template">
                <ControlTemplate>
                  <Grid Name="PART_Root" RowDefinitions="Auto,Auto" ColumnDefinitions="Auto,*">
                    <Border Name="PART_TabsList" Grid.ColumnSpan="2" Background="#f5f5f5" Padding="3" HorizontalAlignment="Left" VerticalAlignment="Top">
                      <ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" />
                    </Border>
                    <ContentPresenter Name="PART_SelectedContentHost" Grid.Row="1" Grid.ColumnSpan="2" Margin="0,8,0,0" Content="{TemplateBinding SelectedContent}" />
                  </Grid>
                </ControlTemplate>
              </Setter>
              <Style Selector="^[TabStripPlacement=Left] /template/ Border#PART_TabsList">
                <Setter Property="Grid.ColumnSpan" Value="1" />
                <Setter Property="Grid.RowSpan" Value="2" />
              </Style>
              <Style Selector="^[TabStripPlacement=Left] /template/ ContentPresenter#PART_SelectedContentHost">
                <Setter Property="Grid.Row" Value="0" />
                <Setter Property="Grid.Column" Value="1" />
                <Setter Property="Grid.ColumnSpan" Value="1" />
                <Setter Property="Margin" Value="8,0,0,0" />
              </Style>
              <Style Selector="^[TabStripPlacement=Left]">
                <Setter Property="ItemsPanel"><ItemsPanelTemplate><StackPanel Spacing="{{spacing}}" /></ItemsPanelTemplate></Setter>
              </Style>
            </ControlTheme>
          </TabControl.Theme>
          <TabControl.ItemContainerTheme>
            <ControlTheme TargetType="TabItem">
              <Setter Property="ClipToBounds" Value="False" />
              <Setter Property="Width" Value="90" />
              <Setter Property="BorderBrush" Value="Transparent" />
              <Setter Property="BorderThickness" Value="1" />
              <Setter Property="Padding" Value="8,4" />
              <Setter Property="FontSize" Value="14" />
              <Setter Property="Template">
                <ControlTemplate>
                  <Panel>
                    <Border Name="PART_LayoutRoot" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                      <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Header}" TextBlock.LineHeight="20" />
                    </Border>
                    <Border Name="PART_SelectedPipe" Background="#ff0000" Height="2" Margin="0,0,0,-5" VerticalAlignment="Bottom" Opacity="0" />
                  </Panel>
                </ControlTemplate>
              </Setter>
              <Style Selector="^:selected /template/ Border#PART_SelectedPipe"><Setter Property="Opacity" Value="{{(line ? 1 : 0)}}" /></Style>
            </ControlTheme>
          </TabControl.ItemContainerTheme>
          <TabItem Header="Account">{{(content ? "<TextBlock Text=\"Your account\" />" : string.Empty)}}</TabItem>
          <TabItem Header="Password"><TextBlock Text="Your password" /></TabItem>
          <TabItem Header="Notifications"><TextBlock Text="Your notifications" /></TabItem>
        </TabControl>
        </Panel>
        """;

    /// <summary>
    /// Where each text's line box starts on both targets: a tab header's from its button's content box (the glyphs Chromium
    /// reports sit inside a line taller than the font, where Avalonia reports the line), the other texts from their glyphs.
    /// </summary>
    private async Task<List<string>> DifferencesAsync(string body)
    {
        var page = await fixture.CompileAsync(body);
        var desktop = Places((await fixture.CaptureInAsync(body, new CaptureOptions("light", 600, 300))).Texts);
        var headers = (await BrowserLayout.EvaluateAsync(page, 600, 300, """
            () => [...document.querySelectorAll("[role=tab]")].map((tab) => {
              const box = tab.getBoundingClientRect(), style = getComputedStyle(tab), px = (value) => parseFloat(value)
              const top = box.top + px(style.borderTopWidth) + px(style.paddingTop), inner = box.height - px(style.borderTopWidth) - px(style.borderBottomWidth) - px(style.paddingTop) - px(style.paddingBottom)
              return [tab.textContent, box.left + px(style.borderLeftWidth) + px(style.paddingLeft), top + (inner - px(style.lineHeight)) / 2].join("\t")
            }).join("\n")
            """)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split('	')).ToDictionary(cells => cells[0], cells => (X: double.Parse(cells[1], CultureInfo.InvariantCulture), Y: double.Parse(cells[2], CultureInfo.InvariantCulture)));
        var web = Places([.. (await BrowserLayout.TextsAsync(page, 600, 300)).Select(text => headers.TryGetValue(text.Text, out var header) ? text with { X = header.X, Y = header.Y } : text)]);
        return desktop.SequenceEqual(web) ? [] : [$"desktop {string.Join(", ", desktop)}{Environment.NewLine}web     {string.Join(", ", web)}"];
    }

    private static ImmutableArray<string> Places(ImmutableArray<LaidOutText> texts) =>
        [.. texts.OrderBy(text => text.Y).ThenBy(text => text.X).Select(text => $"{text.Text}@{Math.Round(text.X)},{Math.Round(text.Y)}")];

    [Fact]
    [Trait("AC", "ITEMS-127")]
    public async Task ThePanelTakesItsHostsMarginAsync()
    {
        var wrong = await DifferencesAsync(Tabs("Top", 0, line: false));
        wrong.AddRange(await DifferencesAsync(Tabs("Left", 0, line: false)));
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    [Trait("AC", "ITEMS-128")]
    public async Task TheTabListTakesItsPanelsSpacingAsync()
    {
        var wrong = await DifferencesAsync(Tabs("Top", 4, line: false, content: false));
        wrong.AddRange(await DifferencesAsync(Tabs("Left", 4, line: false, content: false)));
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    [Trait("AC", "ITEMS-129")]
    public async Task ATabWritesItsPresentersLineHeightAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Wide" TargetType="TabItem">
                  <Setter Property="Width" Value="90" />
                  <Setter Property="Padding" Value="8,4" />
                  <Setter Property="FontSize" Value="14" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" Padding="{TemplateBinding Padding}">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Header}" TextBlock.LineHeight="20" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <TabControl SelectedIndex="0" ItemContainerTheme="{StaticResource Wide}">
                <TabItem Header="One" />
                <TabItem Header="Two" />
                <TabItem Header="Three" />
              </TabControl>
              <TabControl TabStripPlacement="Left" SelectedIndex="0">
                <TabControl.ItemContainerTheme>
                  <ControlTheme TargetType="TabItem">
                    <Setter Property="BorderBrush" Value="Transparent" />
                    <Setter Property="BorderThickness" Value="1" />
                    <Setter Property="Padding" Value="8,4" />
                    <Setter Property="FontSize" Value="14" />
                    <Setter Property="MinHeight" Value="29" />
                    <Setter Property="Template">
                      <ControlTemplate>
                        <Border Name="PART_LayoutRoot" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Header}" TextBlock.LineHeight="20" />
                        </Border>
                      </ControlTemplate>
                    </Setter>
                  </ControlTheme>
                </TabControl.ItemContainerTheme>
                <TabItem Header="Account" />
                <TabItem Header="Password" />
                <TabItem Header="Notifications" />
              </TabControl>
              <TextBlock Text="After" />
            </StackPanel>
            """;
        var wrong = await DifferencesAsync(Body);
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    [Trait("AC", "ITEMS-130")]
    public async Task TheSelectedPipeFollowsItsPartAsync()
    {
        var wrong = new List<string>();
        foreach (var (placement, line) in new[] { ("Top", true), ("Left", true), ("Top", false) })
        {
            var body = Tabs(placement, 4, line, content: false);
            var page = await fixture.CompileAsync(body);
            var desktop = Red((await fixture.CaptureInAsync(body, new CaptureOptions("light", 600, 300))).Png);
            var web = await BrowserLayout.EvaluateAsync(page, 600, 300, """
                () => {
                  const tab = document.querySelector("[role=tab][aria-selected=true]"), after = getComputedStyle(tab, "::after")
                  if (after.display === "none" || after.content === "none") return "none"
                  const box = tab.getBoundingClientRect(), style = getComputedStyle(tab), px = (value) => parseFloat(value)
                  const inner = { left: box.left + px(style.borderLeftWidth), right: box.right - px(style.borderRightWidth), top: box.top + px(style.borderTopWidth), bottom: box.bottom - px(style.borderBottomWidth) }
                  const left = after.left !== "auto" ? inner.left + px(after.left) : inner.right - px(after.right) - px(after.width)
                  const top = after.top !== "auto" ? inner.top + px(after.top) : inner.bottom - px(after.bottom) - px(after.height)
                  return [left, top, px(after.width), px(after.height)].map(Math.round).join(",")
                }
                """);
            wrong.AddRange(desktop == web ? [] : [$"{placement} line {line}: desktop {desktop}, web {web}"]);
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    private static string Red(byte[] png)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        var points = Enumerable.Range(0, bitmap.Width).SelectMany(x => Enumerable.Range(0, bitmap.Height).Select(y => (x, y)))
            .Where(point => bitmap.GetPixel(point.x, point.y) is { Red: > 200, Green: < 60, Blue: < 60 }).ToList();
        return points.Count == 0
            ? "none"
            : string.Create(CultureInfo.InvariantCulture, $"{points.Min(p => p.x)},{points.Min(p => p.y)},{points.Max(p => p.x) - points.Min(p => p.x) + 1},{points.Max(p => p.y) - points.Min(p => p.y) + 1}");
    }

    [Fact]
    [Trait("AC", "ITEMS-131")]
    public async Task ATabTakesItsThemesFocusLookAsync()
    {
        var page = await fixture.CompileAsync("""
            <TabControl SelectedIndex="0">
              <TabControl.ItemContainerTheme>
                <ControlTheme TargetType="TabItem">
                  <Setter Property="BorderBrush" Value="Transparent" />
                  <Setter Property="BorderThickness" Value="1" />
                  <Setter Property="Padding" Value="8,4" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Header}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:focus-visible /template/ Border#PART_LayoutRoot">
                    <Setter Property="BorderBrush" Value="#a1a1a1" />
                    <Setter Property="BoxShadow" Value="0 0 0 1 #a1a1a1, 0 0 0 3 #80a1a1a1" />
                  </Style>
                  <Style Selector="^:selected:focus-visible /template/ Border#PART_LayoutRoot">
                    <Setter Property="BorderBrush" Value="Transparent" />
                  </Style>
                </ControlTheme>
              </TabControl.ItemContainerTheme>
              <TabItem Header="One" />
              <TabItem Header="Two" />
            </TabControl>
            """);

        var looks = await BrowserLayout.EvaluateAsync(page, 400, 200, """
            () => {
              const [one, two] = document.querySelectorAll("[role=tab]"), look = (tab) => { const s = getComputedStyle(tab); return [s.borderTopColor, s.boxShadow, s.outlineStyle].join(" ; ") }
              const rest = look(two)
              one.focus({ focusVisible: true })
              const selected = look(one)
              two.removeAttribute("tabindex")
              two.focus({ focusVisible: true })
              return [rest, selected, look(two)].join(" | ")
            }
            """);

        Assert.Equal(
            "rgba(0, 0, 0, 0) ; none ; none | rgba(0, 0, 0, 0) ; rgb(161, 161, 161) 0px 0px 0px 1px, rgba(161, 161, 161, 0.5) 0px 0px 0px 3px ; none | rgb(161, 161, 161) ; rgb(161, 161, 161) 0px 0px 0px 1px, rgba(161, 161, 161, 0.5) 0px 0px 0px 3px ; none",
            looks);
    }

    [Fact]
    [Trait("AC", "ITEMS-132")]
    public async Task AFluentPanelTakesItsPaddingOnceAsync()
    {
        const string Body = """
            <TabControl SelectedIndex="0" Width="400" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TabItem Header="Inbox"><TextBlock Text="Three unread messages" /></TabItem>
              <TabItem Header="Archive"><TextBlock Text="Nothing archived" /></TabItem>
            </TabControl>
            """;
        var page = await fixture.CompileAsync(Body);
        var desktop = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 600, 300))).Texts.Single(text => text.Text == "Three unread messages");
        var web = (await BrowserLayout.TextsAsync(page, 600, 300)).Single(text => text.Text == "Three unread messages");

        Assert.Equal((Math.Round(desktop.X), Math.Round(desktop.Y)), (Math.Round(web.X), Math.Round(web.Y)));
    }

    private const string Reads = "Padding=\"{TemplateBinding Padding}\"";

    [Fact]
    [Trait("AC", "ITEMS-133")]
    public async Task APaddingAroundTheStripAndThePanelIsWrittenAroundBothAsync()
    {
        static string Body(string reader) => $$"""
            <TabControl SelectedIndex="0" Width="400" Padding="20,10" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TabControl.Theme>
                <ControlTheme TargetType="TabControl">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="#f5f5f5" {{(reader == "around" ? Reads : string.Empty)}}>
                        <DockPanel>
                          <Border DockPanel.Dock="Top" {{(reader == "list" ? Reads : string.Empty)}}>
                            <ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" />
                          </Border>
                          <Border {{(reader == "panel" ? Reads : string.Empty)}}>
                            <ContentPresenter Name="PART_SelectedContentHost" Content="{TemplateBinding SelectedContent}" />
                          </Border>
                        </DockPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Setter Property="ItemsPanel"><ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate></Setter>
                </ControlTheme>
              </TabControl.Theme>
              <TabControl.ItemContainerTheme>
                <ControlTheme TargetType="TabItem">
                  <Setter Property="Width" Value="90" />
                  <Setter Property="BorderBrush" Value="Transparent" />
                  <Setter Property="BorderThickness" Value="1" />
                  <Setter Property="Padding" Value="8,4" />
                  <Setter Property="FontSize" Value="14" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Header}" TextBlock.LineHeight="20" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </TabControl.ItemContainerTheme>
              <TabItem Header="Inbox"><TextBlock Text="Three unread messages" /></TabItem>
              <TabItem Header="Archive"><TextBlock Text="Nothing archived" /></TabItem>
            </TabControl>
            """;

        var wrong = new List<string>();
        foreach (var reader in (string[])["around", "panel", "list"])
        {
            wrong.AddRange((await DifferencesAsync(Body(reader))).Select(difference => $"{reader}: {difference}"));
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }
}
