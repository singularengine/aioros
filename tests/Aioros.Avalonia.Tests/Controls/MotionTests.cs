using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class MotionTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "MOTION-01")]
    public async Task StyleAnimationsBecomeCssAnimationsAsync()
    {
        var page = await fixture.CompileAsync("""
            <Border Classes="pulse" Width="40" Height="40" Background="Red">
              <Border.Styles>
                <Style Selector="Border.pulse">
                  <Style.Animations>
                    <Animation Duration="0:0:1" Delay="0:0:0.2" IterationCount="Infinite" PlaybackDirection="Alternate" FillMode="Both" Easing="CubicEaseOut">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.2" /><Setter Property="Width" Value="20" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /><Setter Property="Background" Value="Blue" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </Border.Styles>
            </Border>
            """);

        Assert.Matches(@"@keyframes (a\w+)\{0%\{opacity:0\.2;width:20px;\}100%\{opacity:1;background-color:#0000ff;\}\}", page.Css);
        Assert.Matches(@"animation:a\w+ 1000ms cubic-bezier\(\.33,1,\.68,1\) 200ms infinite alternate both;", page.Css);
    }

    [Fact]
    [Trait("AC", "MOTION-20")]
    public async Task AnimationsFollowTheSelectorsAvaloniaMatchesAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector=":is(Control).e1">
                  <Style.Animations>
                    <Animation Duration="0:0:0.6" FillMode="Both">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
                <Style Selector="Border.card TextBlock">
                  <Style.Animations>
                    <Animation Duration="0:0:0.3" FillMode="Both">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.5" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </StackPanel.Styles>
              <TextBlock Classes="e1" Text="Entering" />
              <Border Classes="e1" Width="20" Height="20" />
              <TextBlock Text="Still" />
              <Border Classes="card"><TextBlock Text="Carded" /></Border>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css.Split('\n').First(rule => rule.StartsWith("." + Regex.Match(html, "class=\"([a-z0-9]+)\"[^>]*>" + text).Groups[1].Value + "{", StringComparison.Ordinal));
        Assert.Matches("animation:a\\w+ 600ms", Rule("Entering"));
        Assert.Matches("animation:a\\w+ 300ms", Rule("Carded"));
        Assert.DoesNotContain("animation:", Rule("Still"), StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(page.Css, "animation:a\\w+ 600ms"));
    }

    [Fact]
    [Trait("AC", "MOTION-22")]
    public async Task RevealedControlsAreMarkedForTheirScriptAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="Border.enter">
                  <Style.Animations>
                    <Animation Duration="0:0:0.6" FillMode="Both">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </StackPanel.Styles>
              <Border Classes="enter" a:Web.Reveal="True" Width="20" Height="20" />
              <Border Classes="enter" Width="30" Height="30" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);

        Assert.Equal(1, Regex.Count(html, "data-aioros-reveal"));
        var revealed = Regex.Match(html, "<div data-aioros-reveal class=\"([^\"]*)\"").Groups[1].Value.Split(' ');
        Assert.Contains(page.Css.Split('\n'), rule => revealed.Any(name => rule.StartsWith("." + name + "{", StringComparison.Ordinal)) && Regex.IsMatch(rule, "animation:a\\w+ 600ms"));
        Assert.Equal(2, Regex.Count(page.Css, "animation:a\\w+ 600ms"));
    }

    [Fact]
    [Trait("AC", "MOTION-25")]
    public async Task GradientForegroundsPaintTheGlyphsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="Gradient">
                <TextBlock.Foreground>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="#7C3AED" /><GradientStop Offset="1" Color="#06B6D4" /></LinearGradientBrush>
                </TextBlock.Foreground>
              </TextBlock>
              <TextBlock Text="Solid" Foreground="#112233" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => string.Join('\n', Regex.Match(html, "class=\"([^\"]+)\"[^>]*>" + text).Groups[1].Value.Split(' ').Select(name => page.Css.Split('\n').FirstOrDefault(rule => rule.StartsWith("." + name + "{", StringComparison.Ordinal)) ?? string.Empty));
        var gradient = Rule("Gradient");

        Assert.Matches("background:linear-gradient\\(90deg,#7c3aed 0%,#06b6d4 100%\\)", gradient);
        Assert.Contains("background-clip:text;-webkit-background-clip:text;color:transparent;-webkit-text-fill-color:transparent;", gradient, StringComparison.Ordinal);
        Assert.Contains("color:#112233;", Rule("Solid"), StringComparison.Ordinal);
        Assert.DoesNotContain("background-clip", Rule("Solid"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-26")]
    public async Task SweptGradientsMoveAcrossTheTextAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="Swept" a:Web.Sweep="4">
                <TextBlock.Foreground>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="#7C3AED" /><GradientStop Offset="0.5" Color="#06B6D4" /><GradientStop Offset="1" Color="#7C3AED" /></LinearGradientBrush>
                </TextBlock.Foreground>
              </TextBlock>
              <TextBlock Text="Plain" Foreground="#112233" a:Web.Sweep="4" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => string.Join('\n', Regex.Match(html, "class=\"([^\"]+)\"[^>]*>" + text).Groups[1].Value.Split(' ').Select(name => page.Css.Split('\n').FirstOrDefault(rule => rule.StartsWith("." + name + "{", StringComparison.Ordinal)) ?? string.Empty));
        var swept = Rule("Swept");
        var animation = Regex.Match(swept, "animation:(a\\w+) 4000ms linear 0ms infinite");

        Assert.Contains("background-size:200% 100%;background-repeat:repeat-x;", swept, StringComparison.Ordinal);
        Assert.True(animation.Success, swept);
        Assert.Contains("@keyframes " + animation.Groups[1].Value + "{0%{background-position:0 0;}100%{background-position:-200% 0;}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("animation:", Rule("Plain"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-27")]
    public async Task CountingTextsAreMarkedWithTheirDurationAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="85,620" a:Web.CountUp="1.5" />
              <TextBlock Text="7.8×" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);

        Assert.Matches("<div data-aioros-count=\"1500\"[^>]*>85,620</div>", html);
        Assert.Equal(1, Regex.Count(html, "data-aioros-count"));
    }

    [Fact]
    [Trait("AC", "MOTION-29")]
    public async Task PositionSelectorsStaggerAPanelsChildrenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Classes="cards">
              <StackPanel.Styles>
                <Style Selector="StackPanel.cards > :is(Control):nth-child(2)">
                  <Style.Animations>
                    <Animation Duration="0:0:0.5" Delay="0:0:0.1" FillMode="Both"><KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame><KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame></Animation>
                  </Style.Animations>
                </Style>
                <Style Selector="StackPanel.cards > :is(Control):nth-child(3)">
                  <Style.Animations>
                    <Animation Duration="0:0:0.5" Delay="0:0:0.2" FillMode="Both"><KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame><KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame></Animation>
                  </Style.Animations>
                </Style>
                <Style Selector="StackPanel.cards > :is(Control):nth-child(2n+1)">
                  <Style.Animations>
                    <Animation Duration="0:0:0.7"><KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.5" /></KeyFrame><KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame></Animation>
                  </Style.Animations>
                </Style>
                <Style Selector="StackPanel.cards > :is(Control):nth-last-child(1)">
                  <Style.Animations>
                    <Animation Duration="0:0:0.9"><KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.2" /></KeyFrame><KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame></Animation>
                  </Style.Animations>
                </Style>
              </StackPanel.Styles>
              <TextBlock Text="One" />
              <TextBlock Text="Two" />
              <TextBlock Text="Three" />
              <TextBlock Text="Four" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => string.Join('\n', Regex.Match(html, "class=\"([^\"]+)\"[^>]*>" + text + "<").Groups[1].Value.Split(' ').Select(name => page.Css.Split('\n').FirstOrDefault(rule => rule.StartsWith("." + name + "{", StringComparison.Ordinal)) ?? string.Empty));

        Assert.Matches("animation:a\\w+ 700ms", Rule("One"));
        Assert.Matches("animation:a\\w+ 500ms [^ ]+ 100ms", Rule("Two"));
        Assert.Matches("animation:a\\w+ 500ms [^ ]+ 200ms", Rule("Three"));
        Assert.Matches("[:,]a\\w+ 700ms", Rule("Three"));
        Assert.Matches("animation:a\\w+ 900ms", Rule("Four"));
        Assert.DoesNotMatch("700ms", Rule("Two"));
    }

    [Fact]
    [Trait("AC", "MOTION-30")]
    public async Task MarqueesMoveLeftByHalfTheirWidthWithoutEndAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel Orientation="Horizontal" Spacing="8" a:Web.Marquee="30">
                <TextBlock Text="Web" /><TextBlock Text="Windows" /><TextBlock Text="Web" /><TextBlock Text="Windows" />
              </StackPanel>
              <StackPanel Orientation="Horizontal"><TextBlock Text="Still" /></StackPanel>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var marquee = Regex.Match(html, "<div data-aioros-marquee class=\"([^\"]+)\"");
        var rules = string.Join('\n', marquee.Groups[1].Value.Split(' ').Select(name => page.Css.Split('\n').FirstOrDefault(rule => rule.StartsWith("." + name + "{", StringComparison.Ordinal)) ?? string.Empty));
        var animation = Regex.Match(rules, "animation:(a\\w+) 30000ms linear 0ms infinite");

        Assert.True(marquee.Success, html);
        Assert.True(animation.Success, rules);
        Assert.Contains("@keyframes " + animation.Groups[1].Value + "{0%{transform:translateX(0);}100%{transform:translateX(-50%);}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("[data-aioros-marquee]:hover{animation-play-state:paused}", page.Css, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, "data-aioros-marquee"));
    }

    [Fact]
    [Trait("AC", "MOTION-31")]
    public async Task SpotlightCardsCarryTheirGlowAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Width="200" Height="100" CornerRadius="12" Background="#111111" a:Web.Spotlight="#33FFFFFF"><TextBlock Text="Lit" /></Border>
              <Border Width="200" Height="100" Background="#111111"><TextBlock Text="Dark" /></Border>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var lit = Regex.Match(html, "<div data-aioros-spotlight class=\"([^\"]+)\"");
        var rules = string.Join('\n', lit.Groups[1].Value.Split(' ').Select(name => page.Css.Split('\n').FirstOrDefault(rule => rule.StartsWith("." + name + "{", StringComparison.Ordinal)) ?? string.Empty));

        Assert.True(lit.Success, html);
        Assert.Contains("--aioros-spotlight:rgba(255,255,255,0.2);", rules, StringComparison.Ordinal);
        Assert.Contains("[data-aioros-spotlight]::after{", page.Css, StringComparison.Ordinal);
        Assert.Matches(@"\[data-aioros-spotlight\]::after\{[^}]*background:radial-gradient\([^}]*at var\(--x,50%\) var\(--y,50%\),var\(--aioros-spotlight\),transparent", page.Css);
        Assert.Matches(@"\[data-aioros-spotlight\]::after\{[^}]*border-radius:inherit;pointer-events:none", page.Css);
        Assert.Contains("[data-aioros-spotlight]:hover::after{opacity:1}", page.Css, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, "data-aioros-spotlight"));
    }

    [Fact]
    [Trait("AC", "MOTION-33")]
    public async Task ParallaxLayersAreMarkedWithTheirFactorAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Panel>
              <Border Width="100" Height="100" Background="Red" a:Web.Parallax="0.2" />
              <Border Width="100" Height="100" Background="Blue" />
            </Panel>
            """);

        Assert.Matches("<div data-aioros-parallax=\"0.2\" class=", html);
        Assert.Equal(1, Regex.Count(html, "data-aioros-parallax"));
    }

    [Fact]
    [Trait("AC", "MOTION-35")]
    public async Task SizeKeyFramesAreWrittenInPixelsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="Border.grow">
                  <Style.Animations>
                    <Animation Duration="0:0:1" IterationCount="Infinite" PlaybackDirection="Alternate">
                      <KeyFrame Cue="0%"><Setter Property="Height" Value="16" /><Setter Property="Width" Value="0" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Height" Value="70.5" /><Setter Property="Width" Value="345" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </StackPanel.Styles>
              <Border Classes="grow" Background="Red" />
            </StackPanel>
            """);

        Assert.Matches(@"@keyframes a\w+\{0%\{height:16px;width:0px;\}100%\{height:70.5px;width:345px;\}\}", page.Css);
    }

    [Fact]
    [Trait("AC", "MOTION-21")]
    public async Task StyleAnimationsRunAtOnceUnderReducedMotionAsync()
    {
        var page = await fixture.CompileAsync("""
            <TextBlock Classes="e1" Text="Entering">
              <TextBlock.Styles>
                <Style Selector="TextBlock.e1">
                  <Style.Animations>
                    <Animation Duration="0:0:0.6" Delay="0:0:0.2" IterationCount="Infinite" FillMode="Both">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </TextBlock.Styles>
            </TextBlock>
            """);

        var name = Regex.Match(EngineFixture.Body(page), "class=\"([a-z0-9]+)\"[^>]*>Entering").Groups[1].Value;
        Assert.Contains($"@media (prefers-reduced-motion:reduce){{.{name}{{animation-duration:0s!important;animation-delay:0s!important;animation-iteration-count:1!important}}}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-02")]
    public async Task TransformAnimationsBecomeCssTransformsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Classes="bar" Width="8" Height="40" Background="Red" RenderTransformOrigin="50%,100%">
                <Border.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1" /></Border.RenderTransform>
              </Border>
              <Border Classes="spin" Width="8" Height="8" Background="Blue">
                <Border.RenderTransform>
                  <TransformGroup><TranslateTransform X="4" Y="-2" /><RotateTransform Angle="0" /><SkewTransform AngleX="10" /></TransformGroup>
                </Border.RenderTransform>
              </Border>
              <StackPanel.Styles>
                <Style Selector="Border.bar">
                  <Style.Animations>
                    <Animation Duration="0:0:0.6" IterationCount="Infinite">
                      <KeyFrame Cue="0%"><Setter Property="ScaleTransform.ScaleY" Value="0.2" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="ScaleTransform.ScaleY" Value="1" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
                <Style Selector="Border.spin">
                  <Style.Animations>
                    <Animation Duration="0:0:2" IterationCount="Infinite">
                      <KeyFrame Cue="0%"><Setter Property="RotateTransform.Angle" Value="0" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="RotateTransform.Angle" Value="360" /><Setter Property="TranslateTransform.X" Value="8" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </StackPanel.Styles>
            </StackPanel>
            """);

        Assert.Matches(@"@keyframes a\w+\{0%\{transform:scale\(1,0\.2\);\}100%\{transform:scale\(1,1\);\}\}", page.Css);
        Assert.Matches(@"@keyframes a\w+\{0%\{transform:translate\(4px,-2px\) rotate\(0deg\) skew\(10deg,0deg\);\}100%\{transform:translate\(8px,-2px\) rotate\(360deg\) skew\(10deg,0deg\);\}\}", page.Css);
        Assert.Contains("transform-origin:50% 100%;", page.Css, StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "MOTION-03")]
    public async Task TransitionsBecomeCssTransitionsAsync()
    {
        var page = await fixture.CompileAsync("""
            <Border Width="40" Height="40" Background="Red" BorderBrush="Gray" BorderThickness="1">
              <Border.Transitions>
                <Transitions>
                  <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                  <BrushTransition Property="Background" Duration="0:0:0.3" Easing="LinearEasing" />
                  <BrushTransition Property="BorderBrush" Duration="0:0:0.1" Delay="0:0:0.05" />
                  <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.25" Easing="CubicEaseOut" />
                </Transitions>
              </Border.Transitions>
              <TextBlock Text="Hi">
                <TextBlock.Transitions><Transitions><BrushTransition Property="Foreground" Duration="0:0:0.15" /></Transitions></TextBlock.Transitions>
              </TextBlock>
            </Border>
            """);

        Assert.Contains("transition:opacity 200ms linear 0ms,background-color 300ms linear 0ms,border-color 100ms linear 50ms,transform 250ms cubic-bezier(.33,1,.68,1) 0ms;", page.Css, StringComparison.Ordinal);
        Assert.Contains("transition:color 150ms linear 0ms;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-04")]
    public async Task PointerStylesBecomeHoverAndActiveRulesAsync()
    {
        var tile = await fixture.CompileAsync("""
            <Border Classes="tile" Width="120" Height="80" BorderThickness="1">
              <Border.Transitions>
                <Transitions>
                  <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.25" />
                  <BrushTransition Property="Background" Duration="0:0:0.25" />
                </Transitions>
              </Border.Transitions>
              <Border.Styles>
                <Style Selector="Border.tile"><Setter Property="Background" Value="#FFFFFF" /><Setter Property="BorderBrush" Value="#DDDDDD" /></Style>
                <Style Selector="Border.tile TextBlock"><Setter Property="Foreground" Value="#111111" /></Style>
                <Style Selector="Border.tile:pointerover">
                  <Setter Property="Background" Value="#F0F0F0" />
                  <Setter Property="RenderTransform" Value="scale(1.05)" />
                  <Setter Property="BoxShadow" Value="0 8 24 0 #33000000" />
                </Style>
                <Style Selector="Border.tile:pressed"><Setter Property="Opacity" Value="0.8" /></Style>
                <Style Selector="Border.tile:pointerover TextBlock"><Setter Property="Foreground" Value="#3366FF" /></Style>
              </Border.Styles>
              <TextBlock Text="Open" />
            </Border>
            """);
        var page = await fixture.CompileAsync("""<Border Classes="plain" Width="10" Height="10" Background="Red" />""");

        var hover = System.Text.RegularExpressions.Regex.Match(tile.Css, @"\.(h\w+):hover\{background:#f0f0f0!important;transform:scale\(1\.05,1\.05\)!important;box-shadow:[^;]*24px[^;]*!important;\}");
        var child = System.Text.RegularExpressions.Regex.Match(tile.Css, @"\.(h\w+):hover \.(d\w+)\{color:#3366ff!important;\}");
        var body = EngineFixture.Body(tile);

        Assert.True(hover.Success, tile.Css);
        Assert.Matches(@"\.h\w+:active\{opacity:0\.8!important;\}", tile.Css);
        Assert.True(child.Success, tile.Css);
        Assert.Equal(hover.Groups[1].Value, child.Groups[1].Value);
        Assert.Matches("class=\"[^\"]*" + hover.Groups[1].Value, body);
        Assert.Matches("class=\"[^\"]*" + child.Groups[2].Value, body);
        Assert.DoesNotContain(":hover", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-07")]
    public async Task FocusStylesOfContainersBecomeFocusRulesAsync()
    {
        var page = await fixture.CompileAsync("""
            <Border Classes="field" BorderThickness="1">
              <Border.Styles>
                <Style Selector="Border.field"><Setter Property="BorderBrush" Value="#DDDDDD" /></Style>
                <Style Selector="Border.field:focus-within"><Setter Property="BorderBrush" Value="#3366FF" /></Style>
                <Style Selector="Border.field:focus-visible"><Setter Property="BoxShadow" Value="0 0 0 3 #803366FF" /></Style>
              </Border.Styles>
              <TextBox Text="x" />
            </Border>
            """);

        Assert.Matches(@"\.h\w+:focus-within\{border-color:#3366ff!important;\}", page.Css);
        Assert.Matches(@"\.h\w+:focus-visible\{box-shadow:0px 0px 0px 3px rgba\(51,102,255,0\.502\)!important;\}", page.Css);
    }

    [Fact]
    [Trait("AC", "ENGINE-279")]
    public async Task APageTheEngineWritesIsMarkedWrittenAsync()
    {
        var html = await fixture.HtmlAsync("""<TextBlock Text="{Binding $self.(a:Web.IsWritten)}" />""");

        Assert.Contains(">True<", html, StringComparison.Ordinal);
        Assert.False(Aioros.Markup.Web.IsWrittenProperty.GetDefaultValue(typeof(global::Avalonia.Controls.Window)));
    }
}
