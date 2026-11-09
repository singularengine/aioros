using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ButtonThemeTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "BUTTONS-78")]
    public async Task FocusVisibleLooksOfTheThemeAreWrittenAsync()
    {
        var page = await fixture.CompileAsync(Page("""
            <Style Selector="^:focus-visible /template/ ContentPresenter#PART_ContentPresenter">
              <Setter Property="BorderBrush" Value="#999999" />
              <Setter Property="BoxShadow" Value="0 0 0 3 #80999999" />
            </Style>
            """, """<Button Theme="{StaticResource Shad}" Content="Ring" /><Button Content="Fluent" />""", own: """<Setter Property="FocusAdorner" Value="{x:Null}" />"""));

        Assert.Matches(":focus-visible\\{[^}]*border-color:#999999!important;[^}]*\\}", Rules(page, 0));
        Assert.Matches(":focus-visible\\{[^}]*box-shadow:0px 0px 0px 3px rgba\\(153,153,153,0\\.502\\)!important;[^}]*\\}", Rules(page, 0));
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*outline:none;", Rules(page, 0));
        Assert.DoesNotContain(":focus-visible", Rules(page, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("outline", Rules(page, 1), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-79")]
    public async Task TemplatePartShadowsAreWrittenAsync()
    {
        var page = await fixture.CompileAsync(Page("""
            <Style Selector="^ /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="BoxShadow" Value="0 1 2 0 #0d000000, inset 0 0 0 1 #33ff0000" /></Style>
            <Style Selector="^:pointerover /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="BoxShadow" Value="0 4 6 0 #1a000000" /></Style>
            """, """<Button Theme="{StaticResource Shad}" Content="Shadow" />"""));

        var rules = Rules(page, 0);
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*box-shadow:0px 1px 2px 0px rgba\\(0,0,0,0\\.051\\),inset 0px 0px 0px 1px rgba\\(255,0,0,0\\.2\\);", rules);
        Assert.Matches(":hover\\{[^}]*box-shadow:0px 4px 6px 0px rgba\\(0,0,0,0\\.102\\)!important;", rules);
    }

    [Fact]
    [Trait("AC", "BUTTONS-80")]
    public async Task PressedLooksComposeWithPointerOverAsync()
    {
        var hovered = await fixture.CompileAsync(Page(
            """<Style Selector="^:pointerover"><Setter Property="Background" Value="#333333" /></Style>""",
            """<Button Theme="{StaticResource Shad}" Content="Hover" />"""));
        var pressed = await fixture.CompileAsync(Page("""
            <Style Selector="^:pointerover"><Setter Property="Background" Value="#333333" /></Style>
            <Style Selector="^:pressed"><Setter Property="Background" Value="#555555" /></Style>
            <Style Selector="^:pointerover:pressed"><Setter Property="Background" Value="#777777" /></Style>
            """, """<Button Theme="{StaticResource Shad}" Content="Press" />"""));

        var hover = Rules(hovered, 0);
        Assert.Matches(":hover\\{background:#333333!important;\\}", hover);
        Assert.DoesNotContain(":active", hover, StringComparison.Ordinal);
        var press = Rules(pressed, 0).Split('\n');
        var hoverAt = Array.FindIndex(press, rule => rule.EndsWith(":hover{background:#333333!important;}", StringComparison.Ordinal));
        var activeAt = Array.FindIndex(press, rule => rule.EndsWith(":not(:disabled):active{background:#555555!important;}", StringComparison.Ordinal));
        Assert.InRange(hoverAt, 0, activeAt - 1);
        Assert.Contains(press, rule => rule.EndsWith(":not(:disabled):hover:active{background:#777777!important;}", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "BUTTONS-81")]
    public async Task DisabledButtonsHaveNoStateRulesAsync()
    {
        var page = await fixture.CompileAsync(Page("""
            <Style Selector="^:pointerover"><Setter Property="Background" Value="#333333" /></Style>
            <Style Selector="^:pressed"><Setter Property="Background" Value="#555555" /></Style>
            """, """<Button Theme="{StaticResource Shad}" Content="On" /><Button Theme="{StaticResource Shad}" Content="Off" IsEnabled="False" />"""));

        var enabled = Rules(page, 0);
        Assert.Contains(":not(:disabled):hover{background:#333333!important;}", enabled, StringComparison.Ordinal);
        Assert.Contains(":not(:disabled):active{background:#555555!important;}", enabled, StringComparison.Ordinal);
        Assert.DoesNotMatch("\\.s[0-9a-f]+:(hover|active)", enabled);
        Assert.DoesNotContain(":hover", Rules(page, 1), StringComparison.Ordinal);
        Assert.DoesNotContain(":active", Rules(page, 1), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-82")]
    public async Task HyperlinkButtonsFollowTheirThemeAsync()
    {
        var page = await fixture.CompileAsync(Page(
            """<Style Selector="^:pointerover"><Setter Property="Background" Value="#f5f5f5" /></Style>""",
            """
            <HyperlinkButton Theme="{StaticResource Link}" NavigateUri="https://avaloniaui.net/" Content="Outline" />
            <HyperlinkButton NavigateUri="https://avaloniaui.net/" Content="Fluent" />
            """,
            """
            <Setter Property="BorderBrush" Value="#e5e5e5" />
            <Setter Property="BorderThickness" Value="1" />
            """));

        var outline = Rules(page, 0);
        Assert.Contains("border-color:#e5e5e5;", outline, StringComparison.Ordinal);
        Assert.DoesNotContain("underline", outline, StringComparison.Ordinal);
        Assert.DoesNotContain("a.lk:hover", page.Css, StringComparison.Ordinal);
        Assert.Contains("text-decoration:underline;", Rules(page, 1), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-83")]
    public async Task PointerOverStylesSetTextDecorationsAsync()
    {
        var page = await fixture.CompileAsync(Page(
            """<Style Selector="^:pointerover"><Setter Property="TextBlock.TextDecorations" Value="Underline" /></Style>""",
            """<Button Theme="{StaticResource Shad}" Content="Link" />"""));

        var rules = Rules(page, 0);
        Assert.Matches(":hover\\{[^}]*text-decoration:underline!important;", rules);
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*text-decoration:none;", rules);
    }

    [Fact]
    [Trait("AC", "BUTTONS-84")]
    public async Task ContentLineHeightOfTheTemplateIsWrittenAsync()
    {
        var page = await fixture.CompileAsync(Page(string.Empty, """<Button Theme="{StaticResource Shad}" Content="Line" /><Button Content="Fluent" />""", presenter: """TextBlock.LineHeight="20" """));

        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*line-height:20px;", Rules(page, 0));
        Assert.DoesNotContain("line-height", Rules(page, 1), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-05")]
    public async Task TemplatePartTransitionsBecomeCssTransitionsAsync()
    {
        var page = await fixture.CompileAsync(Page(
            """<Style Selector="^:pointerover"><Setter Property="Background" Value="#333333" /></Style>""",
            """<Button Theme="{StaticResource Shad}" Content="Ease" />""",
            presenter: """
                <ContentPresenter.Transitions>
                  <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.15" Easing="0.4,0,0.2,1" />
                    <BoxShadowsTransition Property="BoxShadow" Duration="0:0:0.2" Delay="0:0:0.05" />
                  </Transitions>
                </ContentPresenter.Transitions>
                """,
            own: """<Setter Property="Transitions"><Transitions><TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.075" /></Transitions></Setter>"""));

        var rules = Rules(page, 0);
        Assert.Contains(":hover{background:#333333!important;}", rules, StringComparison.Ordinal);
        Assert.Matches("transition:[^;]*background-color 150ms cubic-bezier\\(0\\.4,0,0\\.2,1\\) 0ms", rules);
        Assert.Matches("transition:[^;]*box-shadow 200ms linear 50ms", rules);
        Assert.Matches("transition:[^;]*transform 75ms linear 0ms", rules);
    }

    [Fact]
    [Trait("AC", "MOTION-06")]
    public async Task NamedEasingsBecomeCssCurvesAsync()
    {
        var page = await fixture.CompileAsync("""
            <Border Width="40" Height="40" Background="Red">
              <Border.Transitions>
                <Transitions>
                  <DoubleTransition Property="Opacity" Duration="0:0:0.2" Easing="QuarticEaseOut" />
                  <DoubleTransition Property="Width" Duration="0:0:0.2" Easing="BackEaseIn" />
                  <DoubleTransition Property="Height" Duration="0:0:0.2" Easing="BounceEaseOut" />
                  <BrushTransition Property="Background" Duration="0:0:0.2" Easing="ElasticEaseOut" />
                </Transitions>
              </Border.Transitions>
            </Border>
            """);

        Assert.Contains("opacity 200ms cubic-bezier(.25,1,.5,1) 0ms", page.Css, StringComparison.Ordinal);
        Assert.Contains("width 200ms cubic-bezier(.36,0,.66,-.56) 0ms", page.Css, StringComparison.Ordinal);
        Assert.Matches("height 200ms linear\\(0,[^)]+,1\\) 0ms", page.Css);
        Assert.Matches("background-color 200ms linear\\(0,[^)]+,1\\) 0ms", page.Css);
    }

    [Fact]
    [Trait("AC", "BUTTONS-85")]
    public async Task BoundObjectContentTakesItsTemplateAsync()
    {
        var first = new Models.Holding(new Models.Toggled("Alpha", true));
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <StackPanel.DataTemplates>
                <DataTemplate DataType="m:Toggled"><TextBlock Text="{Binding Name}" /></DataTemplate>
              </StackPanel.DataTemplates>
              <Button Content="{Binding Item}">
                <Button.ContentTemplate><DataTemplate x:DataType="m:Toggled"><TextBlock Text="{Binding Name, StringFormat='[{0}]'}" /></DataTemplate></Button.ContentTemplate>
              </Button>
              <Button Content="{Binding Item}" />
            </StackPanel>
            """, first, new Models.Holding(new Models.Toggled("Beta", false)));

        var html = EngineFixture.Body(page, first);
        Assert.Contains("[Alpha]", html, StringComparison.Ordinal);
        Assert.Matches(">Alpha<", html);
        Assert.DoesNotContain("Toggled", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-91")]
    public async Task TemplatePartsAroundTheContentAreWrittenAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Pager" TargetType="Button">
                  <Setter Property="Background" Value="Transparent" />
                  <Setter Property="Foreground" Value="#0a0a0a" />
                  <Setter Property="Padding" Value="12,0" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                        <StackPanel Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
                          <Viewbox Width="16" Height="16"><Path Data="M15 18 L9 12 L15 6" Stroke="{TemplateBinding Foreground}" StrokeThickness="2" /></Viewbox>
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" VerticalAlignment="Center" />
                        </StackPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:pointerover"><Setter Property="Background" Value="#f5f5f5" /></Style>
                  <Style Selector="^:focus-visible /template/ Border#PART_Root"><Setter Property="BoxShadow" Value="0 0 0 3 #80999999" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Theme="{StaticResource Pager}" Content="{Binding Name}" />
              <Button Theme="{StaticResource Pager}" Content="Menu"><Button.Flyout><Flyout><TextBlock Text="Open" /></Flyout></Button.Flyout></Button>
              <Button Content="Fluent" />
            </StackPanel>
            """;

        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Models.Item("Back"), new Models.Item("Previous"));

        var html = EngineFixture.Body(page, new Models.Item("Previous"));
        var buttons = Regex.Matches(html, "<button ([^>]*)>(.*?)</button>");
        Assert.StartsWith("type=\"submit\" name=\"_btn\" value=\"0|\"", buttons[0].Groups[1].Value, StringComparison.Ordinal);
        Assert.Matches("^<div[^>]*>.*<svg.*</svg>.*Previous.*</div>$", buttons[0].Groups[2].Value);
        Assert.StartsWith("type=\"button\" popovertarget=", buttons[1].Groups[1].Value, StringComparison.Ordinal);
        Assert.Matches("^<div[^>]*>.*<svg.*</svg>.*Menu.*</div>$", buttons[1].Groups[2].Value);
        Assert.Equal("Fluent", buttons[2].Groups[2].Value);
        var rules = Rules(page, 0);
        Assert.Contains("padding:0px 12px 0px 12px;", rules, StringComparison.Ordinal);
        Assert.Contains(":not(:disabled):hover{background:#f5f5f5!important;}", rules, StringComparison.Ordinal);
        Assert.Matches(":focus-visible\\{[^}]*box-shadow:0px 0px 0px 3px rgba\\(153,153,153,0\\.502\\)!important;", rules);
    }

    [Fact]
    [Trait("AC", "ENGINE-267")]
    public async Task SelfBindingsFollowTheirPropertysBindingAsync()
    {
        const string Body = """
            <StackPanel>
              <m:CopyButton Value="{Binding Code}" Content="Library" />
              <Button Tag="{Binding Code}" a:Web.Copies="{Binding $self.Tag}" Content="Self" />
              <m:CopyButton Value="npm i" Content="Literal" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new OtpModel("pnpm dlx"), new OtpModel("bun x"));
        var other = EngineFixture.Body(page, new OtpModel("bun x"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(2, Regex.Count(other, "data-aioros-copies=\"bun x\""));
        Assert.Equal(2, Regex.Count(EngineFixture.Body(page, new OtpModel("pnpm dlx")), "data-aioros-copies=\"pnpm dlx\""));
        Assert.Contains("data-aioros-copies=\"npm i\"", other, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-113")]
    public async Task CopyingButtonsCopyInTheBrowserAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Copy" TargetType="Button">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="Transparent" Width="24" Height="24">
                        <Panel>
                          <TextBlock Name="PART_Copy" Text="Copy" />
                          <TextBlock Name="PART_Check" Text="Done" Opacity="0" />
                        </Panel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:copied /template/ TextBlock#PART_Copy"><Setter Property="Opacity" Value="0" /></Style>
                  <Style Selector="^:copied /template/ TextBlock#PART_Check"><Setter Property="Opacity" Value="1" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Theme="{StaticResource Copy}" a:Web.Copies="npm install &quot;aioros&quot; &lt;x&gt;" />
              <Button Theme="{StaticResource Copy}" a:Web.Copies="{Binding Code}" />
              <Button Content="Save" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new OtpModel("pnpm dlx"), new OtpModel("bun x"));
        var html = EngineFixture.Body(page, new OtpModel("pnpm dlx"));
        var other = EngineFixture.Body(page, new OtpModel("bun x"));
        string Copied(string text, string opacity)
        {
            var classes = Regex.Match(html, $"class=\"(?<classes>[^\"]+)\">{text}<").Groups["classes"].Value.Split(' ');
            return classes.FirstOrDefault(name => page.Css.Contains($"[data-copied] .{name}{{opacity:{opacity}!important;}}", StringComparison.Ordinal)) ?? $"none for {text}";
        }

        Assert.Matches("<button type=\"button\" data-aioros-copies=\"npm install &quot;aioros&quot; &lt;x&gt;\"", html);
        Assert.Matches("<button type=\"button\" data-aioros-copies=\"pnpm dlx\"", html);
        Assert.Matches("<button type=\"button\" data-aioros-copies=\"bun x\"", other);
        Assert.Single(Regex.Matches(html, "name=\"_btn\""));
        Assert.Matches("name=\"_btn\" value=\"0\\|\"[^>]*>(<[^>]+>)*Save<", html);
        Assert.StartsWith("d", Copied("Copy", "0"), StringComparison.Ordinal);
        Assert.StartsWith("d", Copied("Done", "1"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-269")]
    public async Task AStateLookFollowsTheControlsPlaceAsync()
    {
        var body = Page("""
            <Style Selector="^:focus-visible /template/ ContentPresenter#PART_ContentPresenter">
              <Setter Property="BorderBrush" Value="{DynamicResource Ring}" />
              <Setter Property="BoxShadow" Value="0 0 0 3 #80999999" />
            </Style>
            """, """
            <StackPanel>
              <StackPanel.Resources><SolidColorBrush x:Key="Ring" Color="#999999" /></StackPanel.Resources>
              <StackPanel.Styles>
                <Style Selector="Border.invalid > Button:focus-visible /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="BoxShadow" Value="0 0 0 3 #33ff0000" /></Style>
                <Style Selector="StackPanel[Orientation=Horizontal] > Button:focus-visible /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="BoxShadow" Value="0 0 0 3 #3300ff00" /></Style>
              </StackPanel.Styles>
              <Border><Button Theme="{StaticResource Shad}" Content="Plain" /></Border>
              <StackPanel><Button Theme="{StaticResource Shad}" Content="Column" /></StackPanel>
              <Border Classes="invalid"><Button Theme="{StaticResource Shad}" Content="Invalid" /></Border>
              <StackPanel Orientation="Horizontal"><Button Theme="{StaticResource Shad}" Content="Row" /></StackPanel>
              <Border>
                <Border.Resources><SolidColorBrush x:Key="Ring" Color="#0000ff" /></Border.Resources>
                <Button Theme="{StaticResource Shad}" Content="Own" />
              </Border>
            </StackPanel>
            """);
        var found = new List<string>();
        foreach (var theme in (string[])["light", "dark"])
        {
            var page = await fixture.CompileAsync(body, theme: theme);
            found.AddRange(Enumerable.Range(0, 5).Select(index => $"{theme} {index}: {Focus(Rules(page, index))}"));
        }

        Assert.Equal(
            [
                "light 0: #999999 rgba(153,153,153,0.502)", "light 1: #999999 rgba(153,153,153,0.502)", "light 2: #999999 rgba(255,0,0,0.2)", "light 3: #999999 rgba(0,255,0,0.2)", "light 4: #0000ff rgba(153,153,153,0.502)",
                "dark 0: #999999 rgba(153,153,153,0.502)", "dark 1: #999999 rgba(153,153,153,0.502)", "dark 2: #999999 rgba(255,0,0,0.2)", "dark 3: #999999 rgba(0,255,0,0.2)", "dark 4: #0000ff rgba(153,153,153,0.502)",
            ],
            found);

        static string Focus(string rules) =>
            Regex.Match(rules, ":focus-visible\\{[^}]*border-color:([^!;]+)!important;[^}]*box-shadow:0px 0px 0px 3px ([^!;]+)!important;") is { Success: true } match
                ? $"{match.Groups[1].Value} {match.Groups[2].Value}"
                : "no ring in " + rules;
    }

    [Fact]
    [Trait("AC", "ENGINE-274")]
    public async Task ALookFollowsWhatTheControlFixesItselfAsync()
    {
        const string Plain = """<StackPanel><Button Content="Plain" /></StackPanel>""";
        const string Parts = """
            <StackPanel>
              <Button Content="Rail">
                <Button.Template><ControlTemplate><Panel><Border Width="2" Background="Gray" /><TextBlock Text="{TemplateBinding Content}" /></Panel></ControlTemplate></Button.Template>
              </Button>
            </StackPanel>
            """;
        const string Turned = """<StackPanel><Button Content="Turned"><Button.RenderTransform><RotateTransform Angle="5" /></Button.RenderTransform></Button></StackPanel>""";
        const string Themed = """
            <StackPanel>
              <Button Content="Themed">
                <Button.Theme>
                  <ControlTheme TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                    <Style Selector="^:pressed"><Setter Property="RenderTransform" Value="none" /></Style>
                  </ControlTheme>
                </Button.Theme>
              </Button>
            </StackPanel>
            """;
        var found = new List<string>();
        foreach (var body in (string[])[Plain, Parts, Turned, Themed, Plain, Themed, Turned, Parts])
        {
            var rules = Rules(await fixture.CompileAsync(body), 0);
            var name = Regex.Match(body, "Content=\"([A-Za-z]+)\"").Groups[1].Value;
            found.Add($"{name}: {(rules.Contains("border-color:transparent;border-width:1px 1px 1px 1px;", StringComparison.Ordinal) ? "border" : "no border")}, {(rules.Contains("scale(0.98", StringComparison.Ordinal) ? "scale" : "no scale")}");
        }

        Assert.Equal(
            ["Plain: border, scale", "Rail: no border, no scale", "Turned: border, no scale", "Themed: border, no scale", "Plain: border, scale", "Themed: border, no scale", "Turned: border, no scale", "Rail: no border, no scale"],
            found);
    }

    private static string Page(string styles, string buttons, string theme = "", string presenter = "", string own = "") => $$"""
        <StackPanel>
          <StackPanel.Resources>
            <ControlTheme x:Key="Shad" TargetType="Button">
              <Setter Property="Background" Value="#111111" />
              <Setter Property="Foreground" Value="White" />
              <Setter Property="BorderBrush" Value="Transparent" />
              <Setter Property="BorderThickness" Value="0" />
              <Setter Property="CornerRadius" Value="6" />
              <Setter Property="Padding" Value="16,0" />
              {{own}}
              <Setter Property="Template">
                <ControlTemplate>
                  <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}"
                                    Content="{TemplateBinding Content}" Padding="{TemplateBinding Padding}" Foreground="{TemplateBinding Foreground}" {{(presenter.StartsWith('<') ? ">" + presenter + "</ContentPresenter>" : presenter + "/>")}}
                </ControlTemplate>
              </Setter>
              {{styles}}
            </ControlTheme>
            <ControlTheme x:Key="Link" TargetType="HyperlinkButton" BasedOn="{StaticResource Shad}">{{theme}}</ControlTheme>
          </StackPanel.Resources>
          {{buttons}}
        </StackPanel>
        """;

    private static string Rules(CompiledPage page, int index)
    {
        var classes = Regex.Matches(EngineFixture.Body(page), "<(?:button|a)[^>]* class=\"([^\"]+)\"")[index].Groups[1].Value.Split(' ');
        return string.Join('\n', page.Css.Split('\n').Where(rule => classes.Any(name => rule.StartsWith($".{name}{{", StringComparison.Ordinal) || rule.StartsWith($".{name}:", StringComparison.Ordinal))));
    }

    [Fact]
    [Trait("AC", "BUTTONS-95")]
    public async Task ZeroPaddingOfTheThemeIsWrittenAsync()
    {
        var page = await fixture.CompileAsync(Page(string.Empty, """
            <Button Theme="{StaticResource Bare}" Content="..." /><Button Theme="{StaticResource Shad}" Content="Padded" />
            """).Replace("<StackPanel.Resources>", """
            <StackPanel.Resources>
            <ControlTheme x:Key="Bare" TargetType="Button">
              <Setter Property="Background" Value="Transparent" />
              <Setter Property="Padding" Value="0" />
              <Setter Property="Template">
                <ControlTemplate>
                  <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" Content="{TemplateBinding Content}" Padding="{TemplateBinding Padding}" />
                </ControlTemplate>
              </Setter>
            </ControlTheme>
            """, StringComparison.Ordinal));

        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*padding:0;", Rules(page, 0));
        Assert.DoesNotContain("background", Rules(page, 0), StringComparison.Ordinal);
        Assert.Contains("padding:0px 16px 0px 16px;", Rules(page, 1), StringComparison.Ordinal);
    }

    private const string SiteTheme = """
        <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/SiteMenuTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
        """;

    [Fact]
    [Trait("AC", "BUTTONS-99")]
    public async Task ContentAlignmentOfTheTemplateIsWrittenAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{SiteTheme}}</StackPanel.Resources>
              <Button Theme="{StaticResource SiteMenuLink}" Width="246"><StackPanel><TextBlock Text="Alert Dialog" /><TextBlock Text="A modal dialog" /></StackPanel></Button>
              <Button Theme="{StaticResource SiteMenuLink}" HorizontalContentAlignment="Right" VerticalContentAlignment="Bottom" Content="Right" />
              <Button Theme="{StaticResource SiteMenuLink}" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" Content="Centered" />
              <Button Content="Fluent" />
            </StackPanel>
            """);

        Assert.Contains("display:grid;justify-items:stretch;align-items:stretch;text-align:start;", Rules(page, 0), StringComparison.Ordinal);
        Assert.Contains("display:grid;justify-items:end;align-items:end;text-align:end;", Rules(page, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("display:grid", Rules(page, 2), StringComparison.Ordinal);
        Assert.Contains("display:grid;justify-items:stretch;align-items:stretch;text-align:start;", Rules(page, 3), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-100")]
    public async Task OpenFlyoutLooksOfTheThemeAreWrittenAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{SiteTheme}}</StackPanel.Resources>
              <m:NavigationMenu>
                <m:NavigationMenuTrigger Content="Home"><m:NavigationMenuTrigger.Flyout><Flyout><TextBlock Text="Body" /></Flyout></m:NavigationMenuTrigger.Flyout></m:NavigationMenuTrigger>
              </m:NavigationMenu>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var trigger = Regex.Match(html, "<button type=\"button\" popovertarget=\"m1\" class=\"(s[0-9a-f]+) k[0-9a-f]+\"");
        Assert.True(trigger.Success, html);
        var name = trigger.Groups[1].Value;
        Assert.Matches($"\\.{name}:not\\(:disabled\\):has\\(\\+:popover-open\\)\\{{background:rgba\\(245,245,245,0\\.502\\)!important;color:#171717!important;\\}}", page.Css);
        Assert.Contains($".{name}:not(:disabled):hover:has(+:popover-open){{background:#f5f5f5!important;", page.Css, StringComparison.Ordinal);
        var turned = Regex.Match(page.Css, "\\[popovertarget\\]:has\\(\\+:popover-open\\) \\.(fo[0-9a-f]+)\\{transform:rotate\\(180deg\\)\\}");
        Assert.True(turned.Success, page.Css);
        Assert.Matches($"<svg [^>]*class=\"[^\"]*{turned.Groups[1].Value}[^\"]*\"", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-111")]
    public async Task ButtonsOfPartsLayTheirContentOutAsTheirTemplateDoesAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Width="256">
              <StackPanel.Resources>
                <ControlTheme x:Key="Rooted" TargetType="Button">
                  <Setter Property="Padding" Value="8" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="HorizontalContentAlignment" Value="Left" />
                  <Setter Property="VerticalContentAlignment" Value="Center" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Border" Background="#ffffff" Padding="{TemplateBinding Padding}">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Paneled" TargetType="Button">
                  <Setter Property="Padding" Value="8" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Panel>
                        <Border Name="PART_Border" Background="#ffffff" Padding="{TemplateBinding Padding}">
                          <DockPanel><ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" /></DockPanel>
                        </Border>
                      </Panel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Theme="{StaticResource Rooted}" Content="Rooted" />
              <Button Theme="{StaticResource Rooted}" HorizontalContentAlignment="Center" Content="Centered" />
              <Button Theme="{StaticResource Paneled}" Content="Paneled" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rule(int index) => string.Concat(Regex.Matches(html, "<button [^>]*class=\"(?<class>[^\"]+)\"")[index].Groups["class"].Value.Split(' ').Select(name => Regex.Match(page.Css, $@"\.{name}\{{(?<rule>[^}}]*)\}}").Groups["rule"].Value));

        Assert.Contains("display:grid;justify-items:start;align-items:center;text-align:start;", Rule(0), StringComparison.Ordinal);
        Assert.DoesNotContain("display:grid", Rule(1), StringComparison.Ordinal);
        Assert.Contains("display:grid;justify-items:stretch;align-items:stretch;text-align:start;", Rule(2), StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "BUTTONS-120")]
    public async Task ALinkThemedWithMoreThanItsContentIsWrittenThroughItsTemplateAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Marker" TargetType="HyperlinkButton">
                  <Setter Property="Foreground" Value="#2563eb" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Box" Background="#f5f5f5" CornerRadius="4" Padding="2,0">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                          <Border Width="12" Height="12" Background="#ef4444" />
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                        </StackPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:pointerover /template/ Border#PART_Box"><Setter Property="Background" Value="#e5e5e5" /></Style>
                  <Style Selector="^:pressed"><Setter Property="Foreground" Value="#1e40af" /></Style>
                  <Style Selector="^ /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="TextBlock.TextDecorations" Value="Underline" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <HyperlinkButton Theme="{StaticResource Marker}" NavigateUri="/docs" Content="Read the docs" />
              <HyperlinkButton NavigateUri="/plain" Content="Plain" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var marker = Regex.Match(html, "<a href=\"/docs\" class=\"(?<class>[^\"]+)\">(?<inside>.*?)</a>");
        Assert.True(marker.Success);
        Assert.Contains("background:#ef4444", marker.Groups["inside"].Value + page.Css, StringComparison.Ordinal);
        Assert.Matches("<div [^>]*>.*Read the docs", marker.Groups["inside"].Value);
        var rules = Rules(page, 0);
        Assert.Contains("background:#f5f5f5;", rules, StringComparison.Ordinal);
        Assert.Matches(":hover\\{[^}]*background:#e5e5e5", rules);
        Assert.Contains("text-decoration:none;", rules, StringComparison.Ordinal);
        var text = Regex.Match(marker.Groups["inside"].Value, "class=\"([^\"]+)\"[^>]*>Read the docs").Groups[1].Value.Split(' ');
        Assert.Contains(text, name => Regex.IsMatch(page.Css, $@"\.{name}\{{[^}}]*text-decoration:underline;"));
        Assert.DoesNotContain(text, name => Regex.IsMatch(page.Css, $@"\.{name}\{{[^}}]*color:#2563eb"));
        Assert.Matches(":active\\{color:#1e40af", page.Css);
        Assert.Matches("<a href=\"/plain\" class=\"lk [s0-9a-f ]*k[0-9a-f]+\">Plain</a>", html);
    }
}
