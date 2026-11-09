using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ShellTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "LAYOUT-63")]
    public async Task LibraryPanelsDockedOnTheWebAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <m:Shell a:Web.Layout="Dock" Height="200">
                <m:ShellSide Width="240" DockPanel.Dock="Left"><TextBlock Text="Left side" /></m:ShellSide>
                <Border><TextBlock Text="Left main" /></Border>
              </m:Shell>
              <m:Shell a:Web.Layout="Dock" Height="200">
                <m:ShellSide Width="240" DockPanel.Dock="Right"><TextBlock Text="Right side" /></m:ShellSide>
                <Border><TextBlock Text="Right main" /></Border>
              </m:Shell>
              <m:Shell Height="200">
                <m:ShellSide Width="240"><TextBlock Text="Plain side" /></m:ShellSide>
                <Border><TextBlock Text="Plain main" /></Border>
              </m:Shell>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Equal("grid-template-columns:auto minmax(0,1fr);", Rule(page.Css, Owner(html, "Left side", 2), "grid-template-columns:[^;]+;"));
        Assert.Equal("grid-area:1/1/2/2;", Rule(page.Css, Owner(html, "Left side", 1), "grid-area:[^;]+;"));
        Assert.Equal("grid-area:1/2/2/3;", Rule(page.Css, Owner(html, "Left main", 1), "grid-area:[^;]+;"));
        Assert.Equal("grid-template-columns:minmax(0,1fr) auto;", Rule(page.Css, Owner(html, "Right side", 2), "grid-template-columns:[^;]+;"));
        Assert.Equal("grid-area:1/2/2/3;", Rule(page.Css, Owner(html, "Right side", 1), "grid-area:[^;]+;"));
        Assert.Equal("grid-area:1/1/2/2;", Rule(page.Css, Owner(html, "Right main", 1), "grid-area:[^;]+;"));
        Assert.Equal("grid-area:1/1;", Rule(page.Css, Owner(html, "Plain side", 1), "grid-area:[^;]+;"));
        Assert.Equal("grid-area:1/1;", Rule(page.Css, Owner(html, "Plain main", 1), "grid-area:[^;]+;"));
    }

    [Fact]
    [Trait("AC", "ENGINE-238")]
    public async Task LibraryFlagsFlipInTheBrowserAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="m|ShellSide"><Setter Property="Width" Value="240" /></Style>
                <Style Selector="m|ShellSide:collapsed"><Setter Property="Width" Value="48" /></Style>
                <Style Selector="TextBlock.icon"><Setter Property="IsVisible" Value="False" /></Style>
                <Style Selector="m|ShellSide:collapsed TextBlock.icon"><Setter Property="IsVisible" Value="True" /></Style>
                <Style Selector="m|ShellSide:collapsed TextBlock.label"><Setter Property="IsVisible" Value="False" /></Style>
              </StackPanel.Styles>
              <m:Shell a:Web.Flag="IsOpen" a:Web.Layout="Dock" Height="200">
                <m:ShellSide DockPanel.Dock="Left">
                  <StackPanel><TextBlock Classes="icon" Text="Icon" /><TextBlock Classes="label" Text="Label" /></StackPanel>
                </m:ShellSide>
                <StackPanel Orientation="Horizontal">
                  <m:ShellToggle a:Web.Flips="IsOpen" HotKey="Ctrl+B" Content="Toggle" />
                  <Button Content="Save" />
                  <Button Content="More"><Button.Flyout><Flyout><TextBlock Text="Menu" /></Flyout></Button.Flyout></Button>
                </StackPanel>
              </m:Shell>
              <m:Shell a:Web.Flag="IsOpen" Height="40"><m:ShellSide Rebuilds="True" /></m:Shell>
              <m:Shell a:Web.Flag="Missing" Height="40"><TextBlock Text="Plain" /></m:Shell>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Gauged(1));
        var html = EngineFixture.Body(page, new Gauged(1));

        Assert.Equal(2, Regex.Count(html, "data-aioros-flag=\"IsOpen\" data-aioros-on[ >]"));
        Assert.DoesNotContain("data-aioros-flag=\"Missing\"", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, ">Fresh<"));
        Assert.All(Owner(html, "Fresh", 0).Split(' '), name => Assert.DoesNotMatch($"\\.{name}[^{{]*\\{{display:none;", page.Css));
        Assert.Equal(1, Regex.Count(html, ">Icon<"));
        Assert.Equal(1, Regex.Count(html, ">Label<"));
        Assert.Equal(("width:240px;", "width:48px;"), Flipped(page.Css, Owner(html, "Icon", 2), "width:[^;]+;"));
        Assert.Equal((string.Empty, "display:none;"), Flipped(page.Css, Owner(html, "Label", 0), "display:none;"));
        Assert.Equal(("display:none;", string.Empty), Flipped(page.Css, Owner(html, "Icon", 0), "display:none;"));
        Assert.Matches("<button type=\"button\" data-aioros-flips=\"IsOpen\" aria-expanded=\"true\" data-aioros-key=\"[^\"]+\"", html);
        Assert.Single(Regex.Matches(html, "name=\"_btn\""));
        Assert.Matches("name=\"_btn\" value=\"0\\|\"[^>]*>(<[^>]+>)*Save<", html);
    }

    [Fact]
    [Trait("AC", "ENGINE-261")]
    public async Task FlagCookiesAreNamedOnTheirPanelAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <m:Shell a:Web.Flag="IsOpen" a:Web.FlagCookie="sidebar_state" Height="40"><m:ShellSide /></m:Shell>
              <m:Shell a:Web.Flag="IsOpen" Height="40"><m:ShellSide /></m:Shell>
            </StackPanel>
            """, new Gauged(1));

        Assert.Single(Regex.Matches(html, "data-aioros-flag=\"IsOpen\" data-aioros-on data-aioros-cookie=\"sidebar_state\"[ >]"));
        Assert.Single(Regex.Matches(html, "data-aioros-cookie"));
    }

    [Fact]
    [Trait("AC", "ENGINE-262")]
    public async Task FlagsBecomeSheetsBelowTheirBreakpointAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="m|ShellSide"><Setter Property="Width" Value="240" /></Style>
                <Style Selector="m|ShellSide:collapsed"><Setter Property="Width" Value="48" /></Style>
                <Style Selector="m|ShellSide:narrow"><Setter Property="Width" Value="NaN" /></Style>
                <Style Selector="Border.mask"><Setter Property="IsVisible" Value="False" /></Style>
                <Style Selector="m|ShellSide:sheet Border.mask"><Setter Property="IsVisible" Value="True" /></Style>
              </StackPanel.Styles>
              <m:Shell a:Web.Flag="IsOpen" a:Web.Breakpoint="600" a:Web.Layout="Dock" IsOpen="{Binding Open}" Height="200">
                <m:ShellSide DockPanel.Dock="Left" a:Web.Sheet="IsSheetOpen">
                  <Panel><Border Classes="mask" Background="#80000000" a:Web.Flips="IsOpen" /><TextBlock Text="Side" /></Panel>
                </m:ShellSide>
                <m:ShellToggle a:Web.Flips="IsOpen" Content="Toggle" />
              </m:Shell>
              <m:Shell Height="40"><m:ShellSide><TextBlock Text="Plain" /></m:ShellSide></m:Shell>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new ShellModel(true), new ShellModel(false));
        var html = EngineFixture.Body(page, new ShellModel(true));
        var side = Regex.Match(html, "<dialog data-aioros-sheet class=\"sh600\"><[a-z]+ class=\"(?<classes>[^\"]+)\"").Groups["classes"].Value;
        var mask = Regex.Match(html, "<div (?=[^>]*data-aioros-flips=\"IsOpen\")[^>]*class=\"(?<classes>[^\"]+)\"").Groups["classes"].Value;
        string Under(string media, string classes) => classes.Split(' ').Select(name => Regex.Match(page.Css, $"@media \\({Regex.Escape(media)}\\)\\{{[^@]*?\\.{Regex.Escape(name)}\\.{Regex.Escape(name)}[^{{]*\\{{(?<css>[^}}]*)\\}}")).FirstOrDefault(match => match.Success)?.Groups["css"].Value ?? "none";

        Assert.Matches("data-aioros-flag=\"IsOpen\" data-aioros-on data-aioros-breakpoint=\"600\"[ >]", html);
        Assert.Contains("width:240px;", Under("min-width:600px", side), StringComparison.Ordinal);
        Assert.DoesNotContain("width:240px;", Under("max-width:599.98px", side), StringComparison.Ordinal);
        Assert.Contains("display:none;", Under("min-width:600px", mask), StringComparison.Ordinal);
        Assert.Contains("background:rgba(0,0,0,0.5", Under("max-width:599.98px", mask), StringComparison.Ordinal);
        Assert.DoesNotContain("display:none;", Under("max-width:599.98px", mask), StringComparison.Ordinal);
        Assert.Matches("dialog\\.sh600\\{display:contents", page.Css);
        Assert.Equal("width:240px;", Rule(page.Css, Owner(html, "Plain", 1), "width:240px;"));
    }

    [Fact]
    [Trait("AC", "BUTTONS-110")]
    public async Task ButtonsWhosePartsHaveNoBorderResetTheBrowsersAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Button Width="16" Height="40" Content="Rail">
                <Button.Template><ControlTemplate><Panel><Border Width="2" Background="Gray" /><TextBlock Text="{TemplateBinding Content}" /></Panel></ControlTemplate></Button.Template>
              </Button>
              <Button Content="Framed">
                <Button.Template><ControlTemplate><Border Name="PART_Root" BorderThickness="1" BorderBrush="Gray"><TextBlock Text="{TemplateBinding Content}" /></Border></ControlTemplate></Button.Template>
              </Button>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Equal("border-width:0;padding:0;", Rule(page.Css, Owner(html, "Rail", 2), "border-width:0;padding:0;"));
        Assert.Equal(string.Empty, Rule(page.Css, Owner(html, "Framed", 1), "border-width:0;"));
    }

    [Fact]
    [Trait("AC", "LAYOUT-64")]
    public async Task FixedWidthsInARowDoNotShrinkAsync()
    {
        var page = await fixture.CompileAsync("""
            <Border Width="32" ClipToBounds="True">
              <StackPanel Orientation="Horizontal" Spacing="8">
                <Viewbox Width="16" Height="16"><Rectangle Width="24" Height="24" Fill="Black" /></Viewbox>
                <TextBlock Text="Models" />
              </StackPanel>
            </Border>
            """);
        var html = EngineFixture.Body(page, [null]);
        var viewbox = Regex.Match(html, "<svg viewBox=\"0 0 16 16\"[^>]*class=\"(?<classes>[^\"]+)\"|<svg [^>]*class=\"(?<classes>[^\"]+)\"><foreignObject").Groups["classes"].Value;

        Assert.Equal("flex-shrink:0;", Rule(page.Css, viewbox, "flex-shrink:0;"));
        Assert.Equal(string.Empty, Rule(page.Css, Owner(html, "Models", 0), "flex-shrink:0;"));
        Assert.Matches("<div class=\"c hs k[0-9a-f]+\">", html);
    }

    [Fact]
    [Trait("AC", "ENGINE-239")]
    public async Task BoundLibraryFlagsFlipInTheBrowserAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="m|ShellSide"><Setter Property="Width" Value="240" /></Style>
                <Style Selector="m|ShellSide:collapsed"><Setter Property="Width" Value="48" /></Style>
                <Style Selector="TextBlock.icon"><Setter Property="IsVisible" Value="False" /></Style>
                <Style Selector="m|ShellSide:collapsed TextBlock.icon"><Setter Property="IsVisible" Value="True" /></Style>
                <Style Selector="m|ShellSide:collapsed TextBlock.label"><Setter Property="IsVisible" Value="False" /></Style>
              </StackPanel.Styles>
              <m:Shell a:Web.Flag="IsOpen" a:Web.Layout="Dock" IsOpen="{Binding Open}" Height="200">
                <m:ShellSide DockPanel.Dock="Left">
                  <StackPanel><TextBlock Classes="icon" Text="Icon" /><TextBlock Classes="label" Text="Label" /></StackPanel>
                </m:ShellSide>
                <m:ShellToggle a:Web.Flips="IsOpen" Content="Toggle" />
              </m:Shell>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new ShellModel(true), new ShellModel(false));
        var open = EngineFixture.Body(page, new ShellModel(true));
        var closed = EngineFixture.Body(page, new ShellModel(false));

        Assert.Contains("data-aioros-flag=\"IsOpen\" data-aioros-on", open, StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-on", closed, StringComparison.Ordinal);
        Assert.Equal(open.Replace(" data-aioros-on", string.Empty, StringComparison.Ordinal).Replace("aria-expanded=\"true\"", "aria-expanded=\"false\"", StringComparison.Ordinal).Replace(" checked>", ">", StringComparison.Ordinal), closed);
        Assert.Equal(("width:240px;", "width:48px;"), Flipped(page.Css, Owner(open, "Icon", 2), "width:[^;]+;"));
    }

    [Fact]
    [Trait("AC", "ENGINE-259")]
    public async Task BoundFlagsArePostedAsync()
    {
        const string Body = """
            <StackPanel>
              <m:Shell a:Web.Flag="IsOpen" a:Web.Layout="Dock" IsOpen="{Binding Open}" Height="200">
                <m:ShellSide DockPanel.Dock="Left"><TextBlock Text="Side" /></m:ShellSide>
                <m:ShellToggle a:Web.Flips="IsOpen" Content="Toggle" />
              </m:Shell>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new ShellModel(true), new ShellModel(false));
        var open = EngineFixture.Body(page, new ShellModel(true));
        var closed = EngineFixture.Body(page, new ShellModel(false));
        var (planned, plannedOff) = (new ShellModel(false), new ShellModel(true));
        await PlanForms.ApplyAsync(page.Plan, [planned], ImmutableDictionary<string, string>.Empty.Add("f0", "on"), new AdapterSlotResolver());
        await PlanForms.ApplyAsync(page.Plan, [plannedOff], ImmutableDictionary<string, string>.Empty.Add("_chg", "0"), new AdapterSlotResolver());
        var (engine, engineOff) = (new ShellModel(false), new ShellModel(true));
        await fixture.PostAsync(Body, engine, ("f0", "on"));
        await fixture.PostAsync(Body, engineOff, ("_chg", "0"));
        var literal = await fixture.HtmlAsync(Body.Replace("IsOpen=\"{Binding Open}\"", "IsOpen=\"True\"", StringComparison.Ordinal), new ShellModel(true));

        Assert.Matches("data-aioros-flag=\"IsOpen\" data-aioros-on data-change[^>]*><input type=\"checkbox\" hidden name=\"f0\" checked>", open);
        Assert.Matches("data-aioros-flag=\"IsOpen\" data-change[^>]*><input type=\"checkbox\" hidden name=\"f0\">", closed);
        Assert.Equal((true, false), (planned.Open, plannedOff.Open));
        Assert.Equal((true, false), (engine.Open, engineOff.Open));
        Assert.DoesNotContain("data-change", literal, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"checkbox\"", literal, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-260")]
    public async Task TipsOfTheOtherValueAreWrittenAsync()
    {
        static string Body(bool open) => $"""
            <StackPanel>
              <m:Shell a:Web.Flag="IsOpen" a:Web.Layout="Dock" IsOpen="{open}" Height="200">
                <m:ShellSide DockPanel.Dock="Left" CollapsedTip="Inbox"><TextBlock Text="Side" /></m:ShellSide>
                <m:ShellToggle a:Web.Flips="IsOpen" Content="Toggle" />
              </m:Shell>
            </StackPanel>
            """;
        var open = await fixture.CompileAsync(Body(true));
        var closed = await fixture.CompileAsync(Body(false));

        foreach (var page in new[] { open, closed })
        {
            var html = EngineFixture.Body(page);
            var marker = Regex.Match(html, "<span class=\"tip (f[0-9a-f]+)\"").Groups[1].Value;
            Assert.NotEmpty(marker);
            Assert.True(Regex.IsMatch(html, "<span role=\"tooltip\"[^>]*>.*?Inbox.*?</span>"), html);
            Assert.Contains($":where([data-aioros-flag=\"IsOpen\"][data-aioros-on]) .{marker}>.tt{{display:none}}", page.Css, StringComparison.Ordinal);
        }

        var bound = await fixture.PlansLikeAvaloniaAsync(Body(true).Replace("IsOpen=\"True\"", "IsOpen=\"{Binding Open}\"", StringComparison.Ordinal).Replace("<TextBlock Text=\"Side\" />", "<Button Content=\"More\"><Button.Flyout><Flyout><TextBlock Text=\"Menu\" /></Flyout></Button.Flyout></Button>", StringComparison.Ordinal), new ShellModel(true), new ShellModel(false));
        Assert.Contains(">Inbox<", EngineFixture.Body(bound, new ShellModel(true)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-264")]
    public async Task NestedLibraryFlagsFlipInTheBrowserAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="m|ShellSide"><Setter Property="Width" Value="240" /></Style>
                <Style Selector="m|ShellSide:collapsed"><Setter Property="Width" Value="48" /></Style>
                <Style Selector="m|ShellSide:collapsed StackPanel.sub"><Setter Property="IsVisible" Value="False" /></Style>
                <Style Selector="m|ShellItem:not(:expanded) > StackPanel.sub"><Setter Property="IsVisible" Value="False" /></Style>
              </StackPanel.Styles>
              <m:Shell a:Web.Flag="IsOpen" a:Web.Layout="Dock" Height="300">
                <m:ShellSide DockPanel.Dock="Left">
                  <StackPanel>
                    <m:ShellItem a:Web.Flag="IsExpanded" IsExpanded="True">
                      <Button a:Web.Flips="IsExpanded" Content="Playground" />
                      <StackPanel Classes="sub">
                        <TextBlock Text="History" />
                        <m:ShellItem a:Web.Flag="IsExpanded">
                          <Button a:Web.Flips="IsExpanded" Content="Nested" />
                          <StackPanel Classes="sub"><TextBlock Text="Deep" /></StackPanel>
                        </m:ShellItem>
                      </StackPanel>
                    </m:ShellItem>
                    <m:ShellItem a:Web.Flag="IsExpanded">
                      <Button a:Web.Flips="IsExpanded" Content="Models" />
                      <StackPanel Classes="sub"><TextBlock Text="Genesis" /></StackPanel>
                    </m:ShellItem>
                    <m:ShellItem a:Web.Flag="IsExpanded" IsExpanded="{Binding Open}">
                      <Button a:Web.Flips="IsExpanded" Content="Settings" />
                      <StackPanel Classes="sub"><TextBlock Text="General" /></StackPanel>
                    </m:ShellItem>
                  </StackPanel>
                </m:ShellSide>
                <m:ShellToggle a:Web.Flips="IsOpen" Content="Toggle" />
              </m:Shell>
              <Button Content="Save" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new ShellModel(true), new ShellModel(false));
        var html = EngineFixture.Body(page, new ShellModel(true));
        bool Shown(string text, params bool[] on) => !Chained(page.Css, Owner(html, text, 1), on).Contains("display:none", StringComparison.Ordinal);
        var field = Regex.Match(html, "data-aioros-flag=\"IsExpanded\" data-aioros-on data-change[^>]*><input type=\"checkbox\" hidden name=\"(?<name>f[0-9]+)\" checked>").Groups["name"].Value;
        var posted = new ShellModel(false);
        await PlanForms.ApplyAsync(page.Plan, [posted], ImmutableDictionary<string, string>.Empty.Add(field, "on"), new AdapterSlotResolver());

        Assert.Equal(4, Regex.Count(html, "data-aioros-flag=\"IsExpanded\""));
        Assert.Equal(2, Regex.Count(html, "data-aioros-flag=\"IsExpanded\" data-aioros-on"));
        Assert.Equal((1, 1, 1, 1), (Regex.Count(html, ">History<"), Regex.Count(html, ">Deep<"), Regex.Count(html, ">Genesis<"), Regex.Count(html, ">General<")));
        Assert.Matches("<button type=\"button\" data-aioros-flips=\"IsExpanded\" aria-expanded=\"true\"[^>]*>(<[^>]+>)*Playground<", html);
        Assert.Matches("<button type=\"button\" data-aioros-flips=\"IsExpanded\" aria-expanded=\"false\"[^>]*>(<[^>]+>)*Models<", html);
        Assert.Single(Regex.Matches(html, "name=\"_btn\""));
        Assert.Matches("name=\"_btn\" value=\"0\\|\"[^>]*>(<[^>]+>)*Save<", html);
        Assert.Equal((true, false, false, false), (Shown("History", true, true), Shown("History", true, false), Shown("History", false, true), Shown("History", false, false)));
        Assert.Equal((true, false, false), (Shown("Genesis", true, true), Shown("Genesis", true, false), Shown("Genesis", false, true)));
        Assert.Equal((true, false, false, false), (Shown("Deep", true, true, true), Shown("Deep", true, true, false), Shown("Deep", true, false, true), Shown("Deep", false, true, true)));
        Assert.True(posted.Open);
    }

    [Fact]
    [Trait("AC", "LAYOUT-65")]
    public async Task AScrollViewersThumbIsItsOwnAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Bar" TargetType="ScrollBar">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Width="10">
                        <Track Name="PART_Track" Orientation="{TemplateBinding Orientation}"><Thumb><Thumb.Template><ControlTemplate><Border Background="#e5e5e5" CornerRadius="9999" /></ControlTemplate></Thumb.Template></Thumb></Track>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Area" TargetType="ScrollViewer">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" ClipToBounds="True">
                        <Panel>
                          <ScrollContentPresenter Name="PART_ContentPresenter" />
                          <ScrollBar Name="PART_HorizontalScrollBar" Orientation="Horizontal" Theme="{StaticResource Bar}" />
                          <ScrollBar Name="PART_VerticalScrollBar" Orientation="Vertical" Theme="{StaticResource Bar}" />
                        </Panel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <ScrollViewer Theme="{StaticResource Area}" Height="200" VerticalScrollBarVisibility="Auto">
                <ScrollViewer Height="60" VerticalScrollBarVisibility="Visible" AllowAutoHide="False"><Border Height="400" Background="Red" /></ScrollViewer>
              </ScrollViewer>
            </StackPanel>
            """);

        Assert.Contains("scrollbar-color:#e5e5e5 transparent;", page.Css, StringComparison.Ordinal);
        Assert.DoesNotMatch("scrollbar-color:(?!#e5e5e5)", page.Css);
    }

    [Fact]
    [Trait("AC", "LAYOUT-85")]
    public async Task BarsHiddenAtRestShowUnderThePointerAndHideAfterTheirDelayAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Bar" TargetType="ScrollBar">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Width="10">
                        <Track Name="PART_Track" Orientation="{TemplateBinding Orientation}"><Thumb><Thumb.Template><ControlTemplate><Border Background="#e5e5e5" CornerRadius="9999" /></ControlTemplate></Thumb.Template></Thumb></Track>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Area" TargetType="ScrollViewer">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" ClipToBounds="True">
                        <Panel>
                          <ScrollContentPresenter Name="PART_ContentPresenter" />
                          <ScrollBar Name="PART_VerticalScrollBar" Orientation="Vertical" Theme="{StaticResource Bar}" />
                        </Panel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^ /template/ ScrollBar"><Setter Property="Opacity" Value="0" /></Style>
                  <Style Selector="^:pointerover /template/ ScrollBar"><Setter Property="Opacity" Value="1" /></Style>
                </ControlTheme>
                <ControlTheme x:Key="Shown" TargetType="ScrollViewer" BasedOn="{StaticResource Area}">
                  <Style Selector="^ /template/ ScrollBar"><Setter Property="Opacity" Value="1" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <ScrollViewer a:Web.Id="delayed" Theme="{StaticResource Area}" a:Web.ScrollbarHideDelay="600" Height="100" VerticalScrollBarVisibility="Auto"><Border Height="400" /></ScrollViewer>
              <ScrollViewer a:Web.Id="prompt" Theme="{StaticResource Area}" Height="100" VerticalScrollBarVisibility="Auto"><Border Height="400" /></ScrollViewer>
              <ScrollViewer a:Web.Id="shown" Theme="{StaticResource Shown}" Height="100" VerticalScrollBarVisibility="Auto"><Border Height="400" /></ScrollViewer>
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body);
        var html = EngineFixture.Body(page);
        string Rules(string id)
        {
            var classes = System.Text.RegularExpressions.Regex.Match(html, "id=\"" + id + "\" class=\"([^\"]*)\"").Groups[1].Value.Split(' ');
            return string.Join("\n", page.Css.Split('\n').Where(rule => classes.Any(name => name.Length > 0 && rule.Contains("." + name + "{", StringComparison.Ordinal) || rule.Contains("." + name + ":hover{", StringComparison.Ordinal))));
        }

        Assert.Contains("scrollbar-color:transparent transparent;transition:scrollbar-color 0s linear 600ms;", Rules("delayed"), StringComparison.Ordinal);
        Assert.Contains(":hover{scrollbar-color:#e5e5e5 transparent;transition-delay:0s}", Rules("delayed"), StringComparison.Ordinal);
        Assert.Contains("scrollbar-color:transparent transparent;transition:scrollbar-color 0s linear 0ms;", Rules("prompt"), StringComparison.Ordinal);
        Assert.Contains("scrollbar-color:#e5e5e5 transparent;", Rules("shown"), StringComparison.Ordinal);
        Assert.DoesNotContain(":hover", Rules("shown"), StringComparison.Ordinal);
        var colors = await BrowserLayout.ClockedAsync(page, 800, 600, [], async tab =>
        {
            const string Color = "() => getComputedStyle(document.getElementById('delayed')).scrollbarColor";
            var rest = await tab.EvaluateAsync<string>(Color);
            await tab.HoverAsync("#delayed");
            await tab.WaitForFunctionAsync("() => document.getElementById('delayed').matches(':hover')");
            return (rest, await tab.EvaluateAsync<string>(Color));
        });
        Assert.Equal(("rgba(0, 0, 0, 0) rgba(0, 0, 0, 0)", "rgb(229, 229, 229) rgba(0, 0, 0, 0)"), colors);
    }

    private static string Chained(string css, string classes, bool[] on)
    {
        var chain = string.Join(' ', on.Select((value, index) => $":where\\(\\[data-aioros-flag=\"{(index == 0 ? "IsOpen" : "IsExpanded")}\"\\](?:{(value ? "\\[data-aioros-on\\]" : ":not\\(\\[data-aioros-on\\]\\)")})?\\)"));
        return classes.Split(' ').Select(name => Regex.Match(css, $"(?:^|[{{}},\\n]){chain} \\.{Regex.Escape(name)}\\.{Regex.Escape(name)}[,{{][^{{]*\\{{(?<css>[^}}]*)\\}}")).FirstOrDefault(match => match.Success)?.Groups["css"].Value
            ?? throw new Xunit.Sdk.XunitException($"No rule for {classes} under {string.Join(',', on)}");
    }

    private static (string On, string Off) Flipped(string css, string classes, string pattern)
    {
        var marker = classes.Split(' ').Single(name => css.Contains($"[data-aioros-on]) .{name}", StringComparison.Ordinal));
        string Under(string state) => Regex.Match(css, $":where\\(\\[data-aioros-flag=\"IsOpen\"\\]{Regex.Escape(state)}\\) \\.{marker}[^{{]*\\{{[^}}]*?(?<found>{pattern})").Groups["found"].Value;
        return (Under("[data-aioros-on]"), Under(":not([data-aioros-on])"));
    }

    private static string Owner(string html, string text, int up)
    {
        var stack = new List<string>();
        foreach (var match in Regex.Matches(html[..(html.IndexOf($">{text}<", StringComparison.Ordinal) + 1)], "<(?<tag>[a-z]+)(?<attributes>[^>]*)>|</[a-z]+>").ToList())
        {
            if (match.Value.StartsWith("</", StringComparison.Ordinal))
            {
                stack.RemoveAt(stack.Count - 1);
            }
            else if (match.Groups["tag"].Value is not ("input" or "hr" or "br" or "img"))
            {
                stack.Add(Regex.Match(match.Groups["attributes"].Value, "class=\"(?<classes>[^\"]*)\"").Groups["classes"].Value);
            }
        }

        return stack[^(up + 1)];
    }

    private static string Rule(string css, string classes, string pattern) =>
        classes.Split(' ').Select(name => Regex.Match(css, $"\\.{Regex.Escape(name)}\\{{[^}}]*?(?<found>{pattern})")).FirstOrDefault(match => match.Success)?.Groups["found"].Value ?? string.Empty;
    [Fact]
    [Trait("AC", "ENGINE-282")]
    public async Task AnExpanderBoundToAFlagFollowsItInTheBrowserAsync()
    {
        const string Body = """
            <StackPanel>
              <m:Shell a:Web.Flag="IsOpen" Height="200">
                <StackPanel>
                  <Expander Header="Output" IsExpanded="{Binding $parent[m:Shell].IsOpen, Mode=TwoWay}"><TextBlock Text="10.33.4" /></Expander>
                  <Expander Header="Other"><TextBlock Text="Own" /></Expander>
                  <m:ShellToggle a:Web.Flips="IsOpen" Content="Toggle" />
                </StackPanel>
              </m:Shell>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Gauged(1));
        var html = EngineFixture.Body(page, new Gauged(1));

        var details = Regex.Matches(html, "<details[^>]*>").Select(static match => match.Value).ToList();
        Assert.Equal(2, details.Count);
        Assert.Matches(" open data-aioros-follows=\"IsOpen\" data-aioros-open-on=\"true\">$", details[0]);
        Assert.DoesNotContain("data-change", details[0], StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-follows", details[1], StringComparison.Ordinal);
    }
}
