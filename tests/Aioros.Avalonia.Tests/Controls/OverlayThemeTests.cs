using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class OverlayThemeTests(EngineFixture fixture)
{
    private const string PopTheme = """
        <ControlTheme x:Key="Pop" TargetType="FlyoutPresenter">
          <Setter Property="Background" Value="#ffffff" />
          <Setter Property="BorderBrush" Value="#e5e5e5" />
          <Setter Property="BorderThickness" Value="1" />
          <Setter Property="CornerRadius" Value="8" />
          <Setter Property="Padding" Value="16" />
          <Setter Property="Width" Value="288" />
          <Setter Property="FontSize" Value="14" />
          <Setter Property="Template">
            <ControlTemplate>
              <Border Name="LayoutRoot" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}"
                      BoxShadow="0 4 6 -1 #1a000000, 0 2 4 -2 #1a000000">
                <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" Padding="{TemplateBinding Padding}" />
              </Border>
            </ControlTemplate>
          </Setter>
          <Style Selector="^.wide">
            <Setter Property="Width" Value="320" />
          </Style>
        </ControlTheme>
        """;

    private const string MenuThemes = """
        <ControlTheme x:Key="Surface" TargetType="MenuFlyoutPresenter">
          <Setter Property="Background" Value="#ffffff" />
          <Setter Property="BorderBrush" Value="#e5e5e5" />
          <Setter Property="BorderThickness" Value="1" />
          <Setter Property="CornerRadius" Value="8" />
          <Setter Property="Padding" Value="4" />
          <Setter Property="MinWidth" Value="128" />
          <Setter Property="Template">
            <ControlTemplate>
              <Border Name="LayoutRoot" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" BoxShadow="0 4 6 -1 #1a000000">
                <ItemsPresenter Name="PART_ItemsPresenter" Margin="{TemplateBinding Padding}" ItemsPanel="{TemplateBinding ItemsPanel}" />
              </Border>
            </ControlTemplate>
          </Setter>
        </ControlTheme>
        <ControlTheme x:Key="Item" TargetType="MenuItem">
          <Setter Property="Background" Value="Transparent" />
          <Setter Property="Foreground" Value="#0a0a0a" />
          <Setter Property="Padding" Value="8,6" />
          <Setter Property="CornerRadius" Value="4" />
          <Setter Property="FontSize" Value="14" />
          <Setter Property="Template">
            <ControlTemplate>
              <Panel>
                <Border Name="PART_LayoutRoot" Padding="{TemplateBinding Padding}" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}">
                  <Grid ColumnDefinitions="Auto,*,Auto">
                    <ContentControl Name="PART_IconPresenter" Width="16" Height="16" Margin="0,0,8,0" IsVisible="False" Content="{TemplateBinding Icon}" />
                    <ContentPresenter Name="PART_HeaderPresenter" Grid.Column="1" Content="{TemplateBinding Header}" Foreground="{TemplateBinding Foreground}" TextBlock.LineHeight="20" VerticalAlignment="Center" />
                    <TextBlock Name="PART_InputGestureText" Grid.Column="2" Margin="8,0,0,0" FontSize="12" LetterSpacing="1.2" Foreground="#737373"
                               Text="{Binding $parent[MenuItem].InputGesture}" VerticalAlignment="Center" />
                  </Grid>
                </Border>
                <Ellipse Name="PART_Dot" Width="8" Height="8" Margin="11,0,0,0" HorizontalAlignment="Left" VerticalAlignment="Center" Fill="{TemplateBinding Foreground}" IsVisible="False" />
              </Panel>
            </ControlTemplate>
          </Setter>
          <Style Selector="^:icon /template/ ContentControl#PART_IconPresenter"><Setter Property="IsVisible" Value="True" /></Style>
          <Style Selector="^:radio"><Setter Property="Padding" Value="32,6,8,6" /></Style>
          <Style Selector="^.inset"><Setter Property="Padding" Value="32,6,8,6" /></Style>
          <Style Selector="^:checked:radio /template/ Ellipse#PART_Dot"><Setter Property="IsVisible" Value="True" /></Style>
          <Style Selector="^:selected"><Setter Property="Background" Value="#f5f5f5" /></Style>
          <Style Selector="^.destructive"><Setter Property="Foreground" Value="#e7000b" /></Style>
          <Style Selector="^.label">
            <Setter Property="FontWeight" Value="Medium" />
            <Setter Property="Focusable" Value="False" />
          </Style>
        </ControlTheme>
        """;

    private const string BarTheme = """
        <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/BarTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
        """;

    [Fact]
    [Trait("AC", "NAVIGATION-82")]
    public async Task FlyoutsTakeTheirPresentersThemeAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{PopTheme}}</StackPanel.Resources>
              <Button Content="Open">
                <Button.Flyout>
                  <Flyout FlyoutPresenterTheme="{StaticResource Pop}">
                    <Flyout.FlyoutPresenterClasses><x:String>wide</x:String></Flyout.FlyoutPresenterClasses>
                    <TextBlock Text="Body" />
                  </Flyout>
                </Button.Flyout>
              </Button>
              <Button Content="Fluent"><Button.Flyout><Flyout><TextBlock Text="Plain" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var looks = FlyPattern().Matches(html).Select(match => Rule(page, match.Groups[1].Value)).ToList();
        Assert.Equal(2, looks.Count);
        Assert.StartsWith("width:320px;background:#ffffff;border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-style:solid;border-radius:8px 8px 8px 8px;padding:16px 16px 16px 16px;"
            + "box-shadow:0px 4px 6px -1px rgba(0,0,0,0.102),0px 2px 4px -2px rgba(0,0,0,0.102);font-size:14px;", looks[0], StringComparison.Ordinal);
        Assert.Contains("box-shadow:none;", looks[1], StringComparison.Ordinal);
        Assert.All(looks, look => Assert.DoesNotContain("transition", look, StringComparison.Ordinal));
        Assert.DoesNotContain("animation", page.Css, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, "<div popover id=\"m[12]\" role=\"dialog\""));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-83")]
    public async Task FlyoutsArePlacedAsAvaloniaPlacesThemAsync()
    {
        string[] placements = ["Bottom", "Top", "Left", "Right", "BottomEdgeAlignedLeft", "BottomEdgeAlignedRight", "TopEdgeAlignedLeft", "TopEdgeAlignedRight", "LeftEdgeAlignedTop", "RightEdgeAlignedBottom", "Center"];
        var html = await fixture.HtmlAsync("<StackPanel>" + string.Concat(placements.Select(placement =>
            $"""<Button Content="{placement}"><Button.Flyout><Flyout Placement="{placement}" VerticalOffset="4" HorizontalOffset="2"><TextBlock Text="x" /></Flyout></Button.Flyout></Button>""")) + "</StackPanel>");

        var styles = Regex.Matches(html, "role=\"dialog\" class=\"[^\"]+\" style=\"position-anchor:--m[0-9]+;([^\"]*)\"").Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(
            [
                "position-area:bottom;margin-top:4px;translate:2px 0px;position-try-fallbacks:flip-block;",
                "position-area:top;margin-bottom:-4px;translate:2px 0px;position-try-fallbacks:flip-block;",
                "position-area:left;margin-right:-2px;translate:0px 4px;position-try-fallbacks:flip-inline;",
                "position-area:right;margin-left:2px;translate:0px 4px;position-try-fallbacks:flip-inline;",
                "position-area:bottom span-right;margin-top:4px;translate:2px 0px;position-try-fallbacks:flip-block;",
                "position-area:bottom span-left;margin-top:4px;translate:2px 0px;position-try-fallbacks:flip-block;",
                "position-area:top span-right;margin-bottom:-4px;translate:2px 0px;position-try-fallbacks:flip-block;",
                "position-area:top span-left;margin-bottom:-4px;translate:2px 0px;position-try-fallbacks:flip-block;",
                "position-area:left span-bottom;margin-right:-2px;translate:0px 4px;position-try-fallbacks:flip-inline;",
                "position-area:right span-top;margin-left:2px;translate:0px 4px;position-try-fallbacks:flip-inline;",
                "position-area:center;translate:2px 4px;",
            ],
            styles);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-84")]
    public async Task AttachedFlyoutsOpenOnHoverOrClickAsync()
    {
        var html = await fixture.HtmlAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{PopTheme}}</StackPanel.Resources>
              <Border Width="40" Height="20" Background="Red" m:Hover.OpenDelay="500" m:Hover.CloseDelay="200">
                <FlyoutBase.AttachedFlyout>
                  <Flyout ShowMode="TransientWithDismissOnPointerMoveAway" FlyoutPresenterTheme="{StaticResource Pop}"><TextBlock Text="Card" /></Flyout>
                </FlyoutBase.AttachedFlyout>
              </Border>
              <Border Width="40" Height="20" Background="Blue">
                <FlyoutBase.AttachedFlyout><Flyout><TextBlock Text="Clicked" /></Flyout></FlyoutBase.AttachedFlyout>
              </Border>
              <TextBlock Text="Rest">
                <FlyoutBase.AttachedFlyout><Flyout ShowMode="Transient"><TextBlock Text="Defaults" /></Flyout></FlyoutBase.AttachedFlyout>
              </TextBlock>
            </StackPanel>
            """);

        Assert.Matches("<span class=\"hv\" data-aioros-overlay data-aioros-hover=\"m1\" data-open=\"500\" data-close=\"200\"><div class=\"[^\"]+\"></div>"
            + "<div popover=\"hint\" id=\"m1\" role=\"dialog\" class=\"fly k[0-9a-f]+\" style=\"position-anchor:--hv;[^\"]*\"><div[^>]*>Card</div></div></span>", html);
        Assert.Matches("<span class=\"hv\" data-aioros-overlay data-aioros-click=\"m2\"><div class=\"[^\"]+\"></div><div popover id=\"m2\" role=\"dialog\" class=\"fly k[0-9a-f]+\"[^>]*><div[^>]*>Clicked</div></div></span>", html);
        Assert.Matches("data-aioros-hover=\"m3\" data-open=\"700\" data-close=\"300\"><div[^>]*>Rest</div><div popover=\"hint\" id=\"m3\"", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-85")]
    public async Task ToolTipsTakeTheirThemesBubbleAndArrowAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Tip" TargetType="ToolTip">
                  <Setter Property="Foreground" Value="#fafafa" />
                  <Setter Property="Background" Value="#171717" />
                  <Setter Property="Padding" Value="12,6" />
                  <Setter Property="CornerRadius" Value="6" />
                  <Setter Property="FontSize" Value="12" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Grid RowDefinitions="Auto,10">
                        <Border Name="PART_LayoutRoot" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" TextBlock.LineHeight="16" />
                        </Border>
                        <Border Name="PART_Arrow" Grid.Row="1" Width="10" Height="10" Margin="0,-7,0,0" CornerRadius="2" HorizontalAlignment="Center" VerticalAlignment="Top"
                                Background="{TemplateBinding Background}" RenderTransform="rotate(45deg)" />
                      </Grid>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Save" ToolTip.Placement="Top" ToolTip.VerticalOffset="0" ToolTip.BetweenShowDelay="300">
                <ToolTip.Tip><ToolTip Theme="{StaticResource Tip}" Content="Saves the file" /></ToolTip.Tip>
              </Button>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var match = TipPattern().Match(html);
        Assert.True(match.Success, html);
        Assert.Equal("position-area:top;margin-bottom:10px;position-try-fallbacks:flip-block;", match.Groups[2].Value);
        Assert.Contains("background:#171717;border-width:0;border-radius:6px 6px 6px 6px;padding:6px 12px 6px 12px;box-shadow:none;font-size:12px;font-weight:400;color:#fafafa;line-height:16px;", Rule(page, match.Groups[1].Value), StringComparison.Ordinal);
        Assert.True(Regex.IsMatch(match.Groups[3].Value, "<svg aria-hidden=\"true\" style=\"position:absolute;left:calc\\(50% \\+ -[67]\\.[0-9]+px\\);top:calc\\(100% \\+ -[0-9.]+px\\);overflow:visible;pointer-events:none\"[^>]*>.*<path [^>]*fill=\"#171717\""), match.Groups[3].Value[..200]);
        Assert.Contains("data-between=\"300\"", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-100")]
    public async Task ATipThemesTextWrapIsWrittenOnItsBubbleAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Balanced" TargetType="ToolTip">
                  <Setter Property="a:Web.TextWrap" Value="balance" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" Background="#171717" Padding="12,6">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Save">
                <ToolTip.Tip><ToolTip Theme="{StaticResource Balanced}" Content="Saves the file" /></ToolTip.Tip>
              </Button>
              <Button Content="Open" ToolTip.Tip="Opens a file" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var matches = Regex.Matches(html, "<span role=\"tooltip\" id=\"m[0-9]+\" class=\"tt (k[0-9a-f]+)");
        Assert.Equal(2, matches.Count);
        Assert.Contains("text-wrap:balance;", Rule(page, matches[0].Groups[1].Value), StringComparison.Ordinal);
        Assert.DoesNotContain("text-wrap", Rule(page, matches[1].Groups[1].Value), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-101")]
    public async Task ATipsControlsAreStyledAsInsideTheirToolTipAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="{x:Type ToolTip}" TargetType="ToolTip">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" Background="#171717" Padding="12,6">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <StackPanel.Styles>
                <Style Selector="ToolTip Border.key">
                  <Setter Property="Background" Value="#336699" />
                </Style>
              </StackPanel.Styles>
              <Button Content="Save">
                <ToolTip.Tip>
                  <StackPanel Orientation="Horizontal" Spacing="4">
                    <TextBlock Text="Save Changes" />
                    <ContentControl Content="S">
                      <ContentControl.Template>
                        <ControlTemplate>
                          <Border Classes="key" Padding="4,0"><ContentPresenter Content="{TemplateBinding Content}" /></Border>
                        </ControlTemplate>
                      </ContentControl.Template>
                    </ContentControl>
                  </StackPanel>
                </ToolTip.Tip>
              </Button>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var tip = Regex.Match(html, "<span role=\"tooltip\".*?</span>", RegexOptions.Singleline).Value;
        var key = Regex.Match(tip, "class=\"([^\"]+)\"[^>]*>(?:<[^>]+>)*S<").Groups[1].Value;
        Assert.Contains("Save Changes", tip, StringComparison.Ordinal);
        var bubble = Regex.Match(tip, "class=\"tt (k[0-9a-f]+)").Groups[1].Value;
        Assert.Contains("background:#171717", Rule(page, bubble), StringComparison.Ordinal);
        Assert.Contains(key.Split(' ').Select(name => page.Css.Split('\n').FirstOrDefault(line => line.StartsWith("." + name + "{", StringComparison.Ordinal)) ?? string.Empty), rule => rule.Contains("background:#336699", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-102")]
    public async Task AWrappedTipIsWrittenAtTheWidthAvaloniaLaysItOutAtAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="{x:Type ToolTip}" TargetType="ToolTip">
                  <Setter Property="MaxWidth" Value="320" />
                  <Setter Property="FontSize" Value="14" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" Background="#171717" Padding="12,6">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Long">
                <ToolTip.Tip>
                  <StackPanel><TextBlock TextWrapping="Wrap" Text="A control which pops up a hint when a control is hovered" /></StackPanel>
                </ToolTip.Tip>
              </Button>
              <Button Content="Short">
                <ToolTip.Tip>
                  <StackPanel><TextBlock TextWrapping="Wrap" Text="Hint" /></StackPanel>
                </ToolTip.Tip>
              </Button>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var styles = Regex.Matches(html, "<span role=\"tooltip\" id=\"m[0-9]+\" class=\"[^\"]+\" style=\"([^\"]*)\"").Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(2, styles.Count);
        var width = Regex.Match(styles[0], "(?:^|;)width:([0-9.]+)px").Groups[1].Value;
        Assert.InRange(double.Parse(width, System.Globalization.CultureInfo.InvariantCulture), 200, 310);
        Assert.DoesNotMatch("(?:^|;)width:", styles[1]);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-86")]
    public async Task MenuItemsTakeTheirThemesLooksAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>
                {{MenuThemes}}
              </StackPanel.Resources>
              <Button Content="Open">
                <Button.Flyout>
                  <MenuFlyout FlyoutPresenterTheme="{StaticResource Surface}">
                    <MenuItem Theme="{StaticResource Item}" Classes="label" Header="My Account" />
                    <MenuItem Theme="{StaticResource Item}" Header="Profile" InputGesture="Ctrl+P" Command="{Binding OpenCommand}" />
                    <MenuItem Theme="{StaticResource Item}" Classes="inset" Header="Inset" Command="{Binding OpenCommand}" />
                    <MenuItem Theme="{StaticResource Item}" Classes="destructive" Header="Delete" Command="{Binding OpenCommand}" />
                    <MenuItem Theme="{StaticResource Item}" Header="Top" ToggleType="Radio" GroupName="side" IsChecked="True" />
                  </MenuFlyout>
                </Button.Flyout>
              </Button>
            </StackPanel>
            """, new Editor());

        var html = EngineFixture.Body(page);
        var menu = Regex.Match(html, "role=\"menu\" data-aioros-overlay class=\"menu (k[0-9a-f]+)\"").Groups[1].Value;
        Assert.Contains("min-width:128px;background:#ffffff;border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-style:solid;border-radius:8px 8px 8px 8px;padding:4px 4px 4px 4px;box-shadow:0px 4px 6px -1px rgba(0,0,0,0.102);", Rule(page, menu), StringComparison.Ordinal);
        var label = Regex.Match(html, "<div role=\"none\" class=\"mi (?:s[0-9a-f]+ )?(k[0-9a-f]+)\"><span class=\"mh\">My Account</span></div>");
        Assert.True(label.Success, html);
        Assert.Contains("font-weight:500;", Rule(page, label.Groups[1].Value), StringComparison.Ordinal);
        var profile = Regex.Match(html, "role=\"menuitem\" tabindex=\"-1\" class=\"mi (s[0-9a-f]+) (k[0-9a-f]+)\"><span class=\"mh\">Profile</span><kbd class=\"kg\" style=\"([^\"]+)\">Ctrl\\+P</kbd>");
        Assert.True(profile.Success, html);
        Assert.Contains(":not(:disabled):focus{background:#f5f5f5!important;}", page.Css, StringComparison.Ordinal);
        Assert.Matches("border-radius:4px 4px 4px 4px;.*padding:0 8px 0 8px;min-height:32px;", Rule(page, profile.Groups[2].Value));
        Assert.Contains("font-size:12px;color:#737373;letter-spacing:1.2px;", profile.Groups[3].Value, StringComparison.Ordinal);
        var inset = Regex.Match(html, "class=\"mi s[0-9a-f]+ (k[0-9a-f]+)\"><span class=\"mh\">Inset</span>").Groups[1].Value;
        Assert.Contains("padding:0 16px 0 32px;", Rule(page, inset), StringComparison.Ordinal);
        var delete = Regex.Match(html, "class=\"mi s[0-9a-f]+ (k[0-9a-f]+)\"><span class=\"mh\">Delete</span>").Groups[1].Value;
        Assert.Contains("color:#e7000b;", Rule(page, delete), StringComparison.Ordinal);
        Assert.Matches("role=\"menuitemradio\" aria-checked=\"true\" tabindex=\"-1\" class=\"mi [^\"]+\"><svg aria-hidden=\"true\" style=\"position:absolute;[^\"]*\"[^>]*>.*?<path [^>]*fill=\"#0a0a0a\"[^>]*/>.*?</svg><span class=\"mh\">Top</span>", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-87")]
    public async Task ToggleMenuItemsPostTheirClickAsync()
    {
        const string Body = """
            <Button Content="View">
              <Button.Flyout>
                <MenuFlyout>
                  <MenuItem Header="Wrap" ToggleType="CheckBox" IsChecked="{Binding Wrap, Mode=TwoWay}" />
                  <MenuItem Header="Left" ToggleType="Radio" GroupName="side" IsChecked="True" />
                  <MenuItem Header="Right" ToggleType="Radio" GroupName="side" />
                </MenuFlyout>
              </Button.Flyout>
            </Button>
            """;
        var picks = new MenuPicks();

        var before = await fixture.HtmlAsync(Body, picks);
        var wrapped = await fixture.PostAsync(Body, picks, ("_btn", "0|"));
        var right = await fixture.PostAsync(Body, picks, ("_btn", "2|"));
        var answer = EngineFixture.Body(right.Page!, picks);

        Assert.Contains("value=\"0|\" role=\"menuitemcheckbox\" aria-checked=\"true\"", before, StringComparison.Ordinal);
        Assert.Equal(PostStatus.Rendered, wrapped.Status);
        Assert.False(picks.Wrap);
        Assert.Contains("value=\"0|\" role=\"menuitemcheckbox\" aria-checked=\"false\"", answer, StringComparison.Ordinal);
        Assert.Contains("value=\"1|\" role=\"menuitemradio\" aria-checked=\"false\"", answer, StringComparison.Ordinal);
        Assert.Contains("value=\"2|\" role=\"menuitemradio\" aria-checked=\"true\"", answer, StringComparison.Ordinal);

        const string Bar = $$"""
            <StackPanel>
              <StackPanel.Resources>{{BarTheme}}</StackPanel.Resources>
              <Menu><MenuItem Header="View"><MenuItem Header="Wrap" ToggleType="CheckBox" IsChecked="{Binding Wrap, Mode=TwoWay}" /></MenuItem></Menu>
            </StackPanel>
            """;
        var bar = new MenuPicks();
        var shown = await fixture.HtmlAsync(Bar, bar);
        var flipped = await fixture.PostAsync(Bar, bar, ("_btn", "0|"));

        Assert.Contains("value=\"0|\" role=\"menuitemcheckbox\" aria-checked=\"true\"", shown, StringComparison.Ordinal);
        Assert.False(bar.Wrap);
        Assert.Contains("value=\"0|\" role=\"menuitemcheckbox\" aria-checked=\"false\"", EngineFixture.Body(flipped.Page!, bar), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-93")]
    public async Task MenuBarsTakeTheirThemesLooksAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{BarTheme}}</StackPanel.Resources>
              <Menu><MenuItem Header="File"><MenuItem Header="New Tab" /></MenuItem><MenuItem Header="Edit"><MenuItem Header="Undo" /></MenuItem></Menu>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var bar = Rule(page, Regex.Match(html, "<menu role=\"menubar\" data-aioros-overlay class=\"mb (k[0-9a-f]+)\">").Groups[1].Value);
        Assert.Contains("box-shadow:0px 1px 2px 0px rgba(0,0,0,0.051);", bar, StringComparison.Ordinal);
        Assert.Contains("align-items:center;gap:4px;", bar, StringComparison.Ordinal);
        var item = Regex.Match(html, "<li role=\"none\"><button type=\"button\" role=\"menuitem\" aria-haspopup=\"menu\" class=\"mbi (s[0-9a-f]+) (k[0-9a-f]+)\" popovertarget=\"m1\"");
        Assert.True(item.Success, html);
        Assert.Contains("padding:0 8px 0 8px;min-height:28px;", Rule(page, item.Groups[2].Value), StringComparison.Ordinal);
        Assert.Contains($".{item.Groups[1].Value}:not(:disabled):has(+:popover-open){{background:#f5f5f5!important;color:#171717!important;}}", page.Css, StringComparison.Ordinal);
        var menu = Regex.Match(html, "<div popover id=\"m1\" role=\"menu\" data-aioros-overlay class=\"menu (k[0-9a-f]+)\" style=\"position-anchor:--m1;position-area:bottom span-right;margin-top:8px;translate:-4px 0px;position-try-fallbacks:flip-block;\">");
        Assert.True(menu.Success, html);
        Assert.StartsWith("width:max-content;min-width:192px;background:#ffffff;border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-style:solid;border-radius:8px 8px 8px 8px;padding:4px 4px 4px 4px;"
            + "box-shadow:0px 4px 6px -1px rgba(0,0,0,0.102),0px 2px 4px -2px rgba(0,0,0,0.102);", Rule(page, menu.Groups[1].Value), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-94")]
    public async Task ButtonsMarkedForHoverOpenTheirFlyoutsOnHoverAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Button Content="Home" a:Web.HoverOpenDelay="200" a:Web.HoverCloseDelay="150"><Button.Flyout><Flyout><TextBlock Text="a" /></Flyout></Button.Flyout></Button>
              <Button Content="Docs" a:Web.HoverOpenDelay="300"><Button.Flyout><Flyout><TextBlock Text="b" /></Flyout></Button.Flyout></Button>
              <Button Content="Plain"><Button.Flyout><Flyout><TextBlock Text="c" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);

        Assert.Contains("<button type=\"button\" popovertarget=\"m1\" data-aioros-overlay data-hover-open=\"200\" data-hover-close=\"150\"", html, StringComparison.Ordinal);
        Assert.Contains("<button type=\"button\" popovertarget=\"m2\" data-aioros-overlay data-hover-open=\"300\" data-hover-close=\"150\"", html, StringComparison.Ordinal);
        Assert.Matches("<button type=\"button\" popovertarget=\"m3\" class=", html);
    }

    [Fact]
    [Trait("AC", "ENGINE-230")]
    public async Task ItemTemplateButtonsInAFlyoutRunTheirCommandAsync()
    {
        const string Body = """
            <m:NavigationMenu>
              <m:NavigationMenuTrigger Content="Getting started">
                <m:NavigationMenuTrigger.Flyout>
                  <Flyout>
                    <StackPanel Width="400">
                      <Button Content="Components" Command="{Binding NavigateCommand}" CommandParameter="/docs/components" />
                      <ItemsControl ItemsSource="{Binding Links}">
                        <ItemsControl.ItemTemplate>
                          <DataTemplate x:DataType="m:MenuLink">
                            <Button Content="{Binding Title}" Command="{Binding $parent[ItemsControl].((m:SiteMenu)DataContext).NavigateCommand}" CommandParameter="{Binding Page}" />
                          </DataTemplate>
                        </ItemsControl.ItemTemplate>
                      </ItemsControl>
                    </StackPanel>
                  </Flyout>
                </m:NavigationMenuTrigger.Flyout>
              </m:NavigationMenuTrigger>
            </m:NavigationMenu>
            """;
        var site = new SiteMenu();

        var shown = await fixture.HtmlAsync(Body, site);
        var linked = await fixture.PostAsync(Body, site, ("_btn", "1|/docs/installation"));
        var installation = site.CurrentPage;
        var plain = await fixture.PostAsync(Body, site, ("_btn", "0|/docs/components"));

        Assert.Contains("value=\"1|/docs/installation\"", shown, StringComparison.Ordinal);
        Assert.Equal(PostStatus.Rendered, linked.Status);
        Assert.Equal("/docs/installation", installation);
        Assert.Equal(PostStatus.Rendered, plain.Status);
        Assert.Equal("/docs/components", site.CurrentPage);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-88")]
    public async Task ContextMenusOpenAtThePointerAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Border Width="40" Height="20" Background="Red">
                <Border.ContextMenu><ContextMenu><MenuItem Header="Copy" a:Web.Href="/copy" /></ContextMenu></Border.ContextMenu>
              </Border>
              <Border Width="40" Height="20" Background="Blue">
                <Border.ContextFlyout><MenuFlyout><MenuItem Header="Paste" a:Web.Href="/paste" /></MenuFlyout></Border.ContextFlyout>
              </Border>
              <TextBox Text="plain" />
            </StackPanel>
            """);

        Assert.Matches("<span class=\"ctx\" data-aioros-overlay data-aioros-context=\"m1\"><div class=\"[^\"]+\"></div><div popover id=\"m1\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+ cm\"[^>]*><a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"/copy\"><span class=\"mh\">Copy</span></a></div></span>", html);
        Assert.Matches("<span class=\"ctx\" data-aioros-overlay data-aioros-context=\"m2\"><div class=\"[^\"]+\"></div><div popover id=\"m2\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+ cm\"[^>]*><a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"/paste\"><span class=\"mh\">Paste</span></a></div></span>", html);
        Assert.Equal(2, Regex.Count(html, "class=\"ctx\""));
        Assert.Contains("[popover].cm{position:fixed;inset:auto;left:var(--aioros-x,0);top:var(--aioros-y,0);margin:0}", await CssAsync(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-08")]
    public async Task APageIsWrittenAsItsTransitionsEndAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <m:Entering Width="40" Height="20" Background="Red" />
              <Border Width="40" Height="20" Background="Blue" Opacity="0.5" />
              <NotificationCard Content="Entered" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var entering = Regex.Match(html, "<div class=\"c (en[0-9a-f]+) (k[0-9a-f]+)\"></div>");
        Assert.True(entering.Success, html);
        Assert.DoesNotMatch("(^|;)(opacity|transform):", Rule(page, entering.Groups[2].Value));
        var name = entering.Groups[1].Value;
        Assert.Matches($"@starting-style\\{{\\.{name}\\.{name}\\.{name}\\.{name}\\{{opacity:0[.0-9]*;transform:matrix\\(0\\.9[0-9]*,0,0,0\\.9[0-9]*,0,0\\);\\}}\\}}", page.Css);
        Assert.Single(Regex.Matches(page.Css, "@starting-style"));
        Assert.Contains("opacity:0.5;", page.Css, StringComparison.Ordinal);
        Assert.DoesNotMatch("(^|[;{])opacity:0(\\.0[0-9]*)?;|transform:matrix\\(0\\.85", Regex.Replace(page.Css, "@starting-style[^\n]*\n", string.Empty));
    }

    [Fact]
    [Trait("AC", "MOTION-09")]
    public async Task OverlaysEnterAndLeaveAsTheirThemeTransitionsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Fading" TargetType="FlyoutPresenter">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="LayoutRoot" Background="#ffffff" m:Fade.Enter="True">
                        <Border.Transitions><Transitions><DoubleTransition Property="Opacity" Duration="0:0:0.2" /></Transitions></Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Open"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Fading}"><TextBlock Text="Body" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var match = Regex.Match(html, "role=\"dialog\" class=\"fly (k[0-9a-f]+) (en[0-9a-f]+)\"");
        Assert.True(match.Success, html);
        Assert.Contains("transition:opacity 200ms linear 0ms,display 200ms allow-discrete,overlay 200ms allow-discrete;", Rule(page, match.Groups[1].Value), StringComparison.Ordinal);
        var name = match.Groups[2].Value;
        Assert.Contains($"@starting-style{{.{name}.{name}.{name}.{name}{{opacity:0;}}}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"[popover].{name}:not(:popover-open){{opacity:0;}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-10")]
    public async Task EntrancesAreWrittenHoweverTheClockMovesAsync()
    {
        var page = await fixture.CompileAsync("""<StackPanel><m:QuickEntering Width="40" Height="20" Background="Red" /><m:Aging /></StackPanel>""");

        var html = EngineFixture.Body(page);
        var entering = Regex.Match(html, "<div class=\"c (en[0-9a-f]+) (k[0-9a-f]+)\"></div>");
        Assert.True(entering.Success, html);
        Assert.Contains($"@starting-style{{.{entering.Groups[1].Value}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-11")]
    public async Task EntrancesStandStillForLessMotionAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Fading" TargetType="FlyoutPresenter">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="LayoutRoot" Background="#ffffff" m:Fade.Enter="True">
                        <Border.Transitions><Transitions><DoubleTransition Property="Opacity" Duration="0:0:0.2" /></Transitions></Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <m:Entering Width="40" Height="20" Background="Red" />
              <Button Content="Open"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Fading}"><TextBlock Text="Body" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);

        var names = Regex.Matches(EngineFixture.Body(page), "\\b(en[0-9a-f]+)\\b").Select(match => match.Groups[1].Value).Distinct().ToList();
        Assert.Equal(2, names.Count);
        Assert.All(names, name => Assert.Contains($"@media (prefers-reduced-motion:reduce){{.{name}.{name}.{name}.{name}{{transition:none!important}}}}", page.Css, StringComparison.Ordinal));
        Assert.All(names, name => Assert.Contains($"@starting-style{{.{name}.{name}.{name}.{name}{{", page.Css, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "MOTION-12")]
    public async Task OverlaysScaleFromTheirThemesOriginAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Fading" TargetType="FlyoutPresenter">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="LayoutRoot" Background="#ffffff" RenderTransformOrigin="50%,0%" m:Fade.Enter="True">
                        <Border.Transitions><Transitions><DoubleTransition Property="Opacity" Duration="0:0:0.2" /></Transitions></Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Open"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Fading}"><TextBlock Text="Body" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);
        var centered = await fixture.CompileAsync("""
            <StackPanel>
              <m:Entering Width="40" Height="20" Background="Red" />
            </StackPanel>
            """);

        var name = Regex.Match(EngineFixture.Body(page), "role=\"dialog\" class=\"fly k[0-9a-f]+ (en[0-9a-f]+)\"").Groups[1].Value;
        Assert.Contains($".{name}{{transform-origin:50% 0%}}", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("transform-origin", centered.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-14")]
    public async Task MenuBarsAndSubMenusEnterAsTheirPopupsSurfacesAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{BarTheme}}</StackPanel.Resources>
              <Menu Theme="{StaticResource FadingBar}"><MenuItem Header="File"><MenuItem Header="New Tab" /><MenuItem Header="Share"><MenuItem Header="Email" /></MenuItem></MenuItem></Menu>
            </StackPanel>
            """);

        var menus = Regex.Matches(EngineFixture.Body(page), "<div popover id=\"m[0-9]+\" role=\"menu\"[^>]* class=\"menu k[0-9a-f]+ (en[0-9a-f]+)\"").Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(2, menus.Count);
        Assert.All(menus, name => Assert.Contains($"[popover].{name}:not(:popover-open){{opacity:0;}}", page.Css, StringComparison.Ordinal));
        Assert.All(menus, name => Assert.Contains($".{name}{{transform-origin:0% 0%}}", page.Css, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "MOTION-15")]
    public async Task OverlaysLeaveAsTheirThemeClosesThemAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Closing" TargetType="FlyoutPresenter">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="LayoutRoot" Background="#ffffff" m:Fade.Enter="True" m:Fade.Lift="8">
                        <Border.Transitions>
                          <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.2" />
                          </Transitions>
                        </Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:closing /template/ Border#LayoutRoot">
                    <Setter Property="Opacity" Value="0" />
                    <Setter Property="RenderTransform" Value="scale(0.95)" />
                    <Setter Property="Transitions">
                      <Transitions>
                        <DoubleTransition Property="Opacity" Duration="0:0:0.1" />
                        <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.1" />
                      </Transitions>
                    </Setter>
                  </Style>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Open"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Closing}"><TextBlock Text="Body" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);

        var name = Regex.Match(EngineFixture.Body(page), "role=\"dialog\" class=\"fly k[0-9a-f]+ (en[0-9a-f]+)\"").Groups[1].Value;
        Assert.Contains($"@starting-style{{.{name}.{name}.{name}.{name}{{opacity:0;transform:matrix(1,0,0,1,0,-8);}}}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"[popover].{name}:not(:popover-open){{opacity:0;transform:matrix(0.95,0,0,0.95,0,0);transition:opacity 100ms linear 0ms,transform 100ms linear 0ms,display 100ms allow-discrete,overlay 100ms allow-discrete;}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-19")]
    public async Task ATipEntersAndLeavesAsItsThemeMovesItAsync()
    {
        var moved = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="{x:Type ToolTip}" TargetType="ToolTip">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Background="#171717" Padding="6" RenderTransformOrigin="50%,100%" m:Fade.Enter="True" m:Fade.Lift="8">
                        <Border.Transitions>
                          <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.15" Easing="0.25,0.1,0.25,1" />
                            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.15" Easing="0.25,0.1,0.25,1" />
                          </Transitions>
                        </Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:closing /template/ Border">
                    <Setter Property="Opacity" Value="0" />
                    <Setter Property="RenderTransform" Value="scale(0.95)" />
                  </Style>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Save" ToolTip.Tip="Saves the draft" />
            </StackPanel>
            """);
        var plain = await fixture.CompileAsync("""<Button Content="Save" ToolTip.Tip="Saves the draft" />""");

        var name = Regex.Match(EngineFixture.Body(moved), "role=\"tooltip\" id=\"[^\"]+\" class=\"tt [^\"]*(tm[0-9a-f]+)").Groups[1].Value;
        Assert.NotEmpty(name);
        Assert.Contains($".tt.{name}{{opacity:0;transform:matrix(0.95,0,0,0.95,0,0);transform-origin:50% 100%;transition:opacity 150ms cubic-bezier(0.25,0.1,0.25,1) 0ms,transform 150ms cubic-bezier(0.25,0.1,0.25,1) 0ms,visibility 0s linear 150ms}}", moved.Css, StringComparison.Ordinal);
        Assert.Contains($".tip:hover>.tt.{name},.tip:has(:focus-visible)>.tt.{name}{{opacity:1;transform:none;transition:visibility 0s linear var(--tip-delay,.4s);animation:k{name} 150ms cubic-bezier(0.25,0.1,0.25,1) var(--tip-delay,.4s) backwards}}", moved.Css, StringComparison.Ordinal);
        Assert.Contains($"@keyframes k{name}{{from{{opacity:0;transform:matrix(1,0,0,1,0,-8);}}}}", moved.Css, StringComparison.Ordinal);
        Assert.Contains($".tip[data-off]>.tt.{name}{{transition:none;animation:none}}", moved.Css, StringComparison.Ordinal);
        Assert.Contains($"@media (prefers-reduced-motion:reduce){{.tt.{name},.tip:hover>.tt.{name},.tip:has(:focus-visible)>.tt.{name}{{transition:visibility 0s;animation:none}}}}", moved.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("@keyframes ktm", plain.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-99")]
    public async Task SwitchingPopoversSlideBetweenEachOtherAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Orientation="Horizontal">
              <StackPanel.Resources>
                <ControlTheme x:Key="Sliding" TargetType="FlyoutPresenter">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="LayoutRoot" Background="#ffffff" m:Fade.Enter="True" m:Fade.Lift="8">
                        <Border.Transitions>
                          <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.2" />
                          </Transitions>
                        </Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:closing /template/ Border#LayoutRoot">
                    <Setter Property="Opacity" Value="0" />
                    <Setter Property="RenderTransform" Value="scale(0.95)" />
                  </Style>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Home" a:Web.HoverOpenDelay="200" a:Web.SwitchOffset="208"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Sliding}"><TextBlock Text="One" /></Flyout></Button.Flyout></Button>
              <Button Content="Docs" a:Web.HoverOpenDelay="200" a:Web.SwitchOffset="208"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Sliding}"><TextBlock Text="Two" /></Flyout></Button.Flyout></Button>
              <Button Content="Plain" a:Web.HoverOpenDelay="200"><Button.Flyout><Flyout FlyoutPresenterTheme="{StaticResource Sliding}"><TextBlock Text="Three" /></Flyout></Button.Flyout></Button>
            </StackPanel>
            """);

        var body = EngineFixture.Body(page);
        var buttons = Regex.Matches(body, "<button[^>]*popovertarget[^>]*>").Select(match => match.Value).ToList();
        var name = Regex.Match(body, "role=\"dialog\" class=\"fly k[0-9a-f]+ (en[0-9a-f]+)\"").Groups[1].Value;
        Assert.Equal([true, true, false], buttons.Select(button => button.Contains(" data-switch", StringComparison.Ordinal)));
        var starting = $".{name}.{name}.{name}.{name}";
        Assert.Contains($"@starting-style{{{starting}[data-motion=from-end]{{opacity:0;transform:translateX(208px) matrix(1,0,0,1,0,-8);}}{starting}[data-motion=from-start]{{opacity:0;transform:translateX(-208px) matrix(1,0,0,1,0,-8);}}}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"[popover].{name}[data-motion=to-start]:not(:popover-open){{opacity:0;transform:translateX(-208px) matrix(0.95,0,0,0.95,0,0);", page.Css, StringComparison.Ordinal);
        Assert.Contains($"[popover].{name}[data-motion=to-end]:not(:popover-open){{opacity:0;transform:translateX(208px) matrix(0.95,0,0,0.95,0,0);", page.Css, StringComparison.Ordinal);
    }

    private async Task<string> CssAsync() =>
        (await fixture.CompileAsync("""<Border Width="4" Height="4"><Border.ContextMenu><ContextMenu><MenuItem Header="x" /></ContextMenu></Border.ContextMenu></Border>""")).Css;

    private static string Rule(CompiledPage page, string name)
    {
        var match = Regex.Match(page.Css, "\\." + name + "\\{([^}]*)\\}");
        Assert.True(match.Success, name + " in " + page.Css);
        return match.Groups[1].Value;
    }

    [GeneratedRegex("role=\"dialog\" class=\"fly (k[0-9a-f]+)\"")]
    private static partial Regex FlyPattern();

    [GeneratedRegex("<span role=\"tooltip\" id=\"m1\" class=\"tt (k[0-9a-f]+) k[0-9a-f]+\" style=\"([^\"]*)\">Saves the file(<svg.*?</svg>)</span>")]
    private static partial Regex TipPattern();
}
