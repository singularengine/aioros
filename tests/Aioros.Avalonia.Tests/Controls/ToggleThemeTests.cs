using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ToggleThemeTests(EngineFixture fixture)
{
    private const string CheckTheme = """
        <ControlTheme x:Key="Check" TargetType="CheckBox">
          <Setter Property="BorderBrush" Value="#e5e5e5" />
          <Setter Property="BorderThickness" Value="1" />
          <Setter Property="CornerRadius" Value="4" />
          <Setter Property="Padding" Value="12,0,0,0" />
          <Setter Property="MinHeight" Value="16" />
          <Setter Property="FocusAdorner" Value="{x:Null}" />
          <Setter Property="Template">
            <ControlTemplate>
              <Grid ColumnDefinitions="Auto,*">
                <Border Name="PART_Border" Width="16" Height="16" VerticalAlignment="Center" Background="Transparent"
                        BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="{TemplateBinding CornerRadius}" BoxShadow="0 1 2 0 #0d000000">
                  <Path Name="CheckGlyph" Width="14" Height="14" Stretch="None" IsVisible="False" Data="M11.667,3.5 L5.25,9.917 L2.333,7"
                        StrokeThickness="1.167" StrokeLineCap="Round" StrokeJoin="Round" Stroke="#fafafa" />
                </Border>
                <ContentPresenter Name="PART_ContentPresenter" Grid.Column="1" Margin="{TemplateBinding Padding}" Content="{TemplateBinding Content}"
                                  TextBlock.LineHeight="14" IsVisible="{TemplateBinding Content, Converter={x:Static ObjectConverters.IsNotNull}}" />
              </Grid>
            </ControlTemplate>
          </Setter>
          <Style Selector="^:checked">
            <Setter Property="BorderBrush" Value="#171717" />
          </Style>
          <Style Selector="^:checked /template/ Border#PART_Border">
            <Setter Property="Background" Value="#171717" />
          </Style>
          <Style Selector="^:checked /template/ Path#CheckGlyph">
            <Setter Property="IsVisible" Value="True" />
          </Style>
          <Style Selector="^:focus-visible /template/ Border#PART_Border">
            <Setter Property="BoxShadow" Value="0 0 0 3 #80a1a1a1" />
          </Style>
        </ControlTheme>
        """;

    [Fact]
    [Trait("AC", "BUTTONS-86")]
    public async Task CheckBoxesDrawTheirThemesMarkAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{CheckTheme}}</StackPanel.Resources>
              <CheckBox Theme="{StaticResource Check}" Content="Accept" IsChecked="True" />
              <CheckBox Content="Fluent" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var themed = Rules(page, html, 0);
        Assert.Matches(">input\\{appearance:none;[^}]*width:16px;height:16px;[^}]*background-color:transparent;border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-radius:4px 4px 4px 4px;box-shadow:0px 1px 2px 0px rgba\\(0,0,0,0\\.051\\);background-image:none;", themed);
        Assert.Matches(":has\\(>input:checked\\)>input\\{[^}]*background-color:#171717;border-color:#171717;[^}]*background-image:url\\(\"data:image/svg\\+xml,%3Csvg[^\"]*stroke='%23fafafa'", themed);
        Assert.Matches(":has\\(>input:focus-visible\\)>input\\{box-shadow:0px 0px 0px 3px rgba\\(161,161,161,0\\.502\\);\\}", themed);
        Assert.Contains("outline:none;", themed, StringComparison.Ordinal);
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*display:flex;flex-direction:row;align-items:center;gap:12px;min-height:16px;line-height:14px;", themed);
        Assert.Matches("<label class=\"[^\"]*mk[0-9a-f]+[^\"]*\"[^>]*><input type=\"checkbox\"[^>]* checked>Accept</label>", html);
        var fluent = Rules(page, html, 1);
        Assert.Matches(">input\\{appearance:none;[^}]*width:20px;height:20px;", fluent);
        Assert.Matches("gap:8px;", fluent);
    }

    [Fact]
    [Trait("AC", "BUTTONS-87")]
    public async Task RadioButtonsDrawTheirThemesCircleAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Radio" TargetType="RadioButton">
                  <Setter Property="BorderBrush" Value="#e5e5e5" />
                  <Setter Property="BorderThickness" Value="1" />
                  <Setter Property="Padding" Value="12,0,0,0" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Grid ColumnDefinitions="Auto,*">
                        <Border Name="PART_Border" Width="16" Height="16" CornerRadius="8" VerticalAlignment="Center" Background="Transparent"
                                BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                          <Ellipse Name="CheckGlyph" Width="8" Height="8" IsVisible="False" Fill="#171717" />
                        </Border>
                        <ContentPresenter Name="PART_ContentPresenter" Grid.Column="1" Margin="{TemplateBinding Padding}" Content="{TemplateBinding Content}" />
                      </Grid>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:checked /template/ Ellipse#CheckGlyph"><Setter Property="IsVisible" Value="True" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <RadioButton Theme="{StaticResource Radio}" Content="Default" IsChecked="True" />
              <RadioButton Theme="{StaticResource Radio}" Content="Compact" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var rules = Rules(page, html, 0);
        Assert.Matches(">input\\{appearance:none;[^}]*width:16px;height:16px;[^}]*border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-radius:8px 8px 8px 8px;[^}]*background-image:none;", rules);
        Assert.Matches(":has\\(>input:checked\\)>input\\{background-image:url\\(\"data:image/svg\\+xml,[^\"]*fill='%23171717'", rules);
        Assert.Matches("gap:12px;", rules);
        Assert.Matches("<input type=\"radio\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"0\" checked>Default</label>", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-88")]
    public async Task ToggleSwitchesDrawTheirThemesTrackAndKnobAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Switch" TargetType="ToggleSwitch">
                  <Setter Property="Padding" Value="8,0,0,0" />
                  <Setter Property="OnContent" Value="{x:Null}" />
                  <Setter Property="OffContent" Value="{x:Null}" />
                  <Setter Property="KnobTransitions">
                    <Transitions><DoubleTransition Property="Canvas.Left" Duration="0:0:0.15" Easing="0.4,0,0.2,1" /></Transitions>
                  </Setter>
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Grid ColumnDefinitions="Auto,*">
                        <Panel Name="SwitchArea" VerticalAlignment="Center">
                          <Border Name="OuterBorder" Width="32" Height="18" CornerRadius="9" BorderThickness="1" Background="#e5e5e5" BorderBrush="Transparent" />
                          <Canvas Name="PART_SwitchKnob" Width="14" Height="16" Margin="1,0,0,0" HorizontalAlignment="Left" VerticalAlignment="Center">
                            <Panel Name="PART_MovingKnobs" Width="16" Height="16"><Ellipse Name="SwitchKnob" Fill="#ffffff" /></Panel>
                          </Canvas>
                        </Panel>
                        <ContentPresenter Name="PART_ContentPresenter" Grid.Column="1" Margin="{TemplateBinding Padding}" Content="{TemplateBinding Content}" />
                        <ContentPresenter Name="PART_OffContentPresenter" IsVisible="False" Content="{TemplateBinding OffContent}" />
                        <ContentPresenter Name="PART_OnContentPresenter" IsVisible="False" Content="{TemplateBinding OnContent}" />
                      </Grid>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:checked /template/ Border#OuterBorder"><Setter Property="Background" Value="#171717" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <ToggleSwitch Theme="{StaticResource Switch}" Content="Airplane mode" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var rules = Rules(page, html, 0);
        Assert.Matches("<label class=\"[^\"]+\"><span class=\"tsr\"><input type=\"checkbox\" role=\"switch\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"on\"></span><span>Airplane mode</span></label>", html);
        Assert.Matches(">\\.tsr>input\\{appearance:none;[^}]*width:32px;height:18px;[^}]*background-color:#e5e5e5;[^}]*border-radius:9px 9px 9px 9px;[^}]*background-image:url\\(\"data:image/svg\\+xml,[^\"]*fill='%23ffffff'[^;]*;background-position:1px 1px;background-size:16px 16px;[^}]*transition:background-position 150ms cubic-bezier\\(0\\.4,0,0\\.2,1\\) 0ms;", rules);
        Assert.Matches(":has\\(>\\.tsr>input:checked\\)>\\.tsr>input\\{background-color:#171717;background-position:15px 1px;\\}", rules);
        Assert.Matches("display:flex;flex-direction:row;align-items:center;gap:8px;", rules);
    }

    [Fact]
    [Trait("AC", "BUTTONS-89")]
    public async Task ToggleButtonStatesFollowTheThemePerClassAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Toggle" TargetType="ToggleButton">
                  <Setter Property="Background" Value="Transparent" />
                  <Setter Property="Foreground" Value="#0a0a0a" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" Foreground="{TemplateBinding Foreground}" Content="{TemplateBinding Content}" />
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:pointerover"><Setter Property="Background" Value="#f5f5f5" /><Setter Property="Foreground" Value="#737373" /></Style>
                  <Style Selector="^:checked"><Setter Property="Background" Value="#f5f5f5" /><Setter Property="Foreground" Value="#171717" /></Style>
                  <Style Selector="^.outline:pointerover"><Setter Property="Foreground" Value="#262626" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <ToggleButton Theme="{StaticResource Toggle}" Content="B" IsChecked="True" />
              <ToggleButton Theme="{StaticResource Toggle}" Classes="outline" Content="I" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var plain = Rules(page, html, 0);
        var outline = Rules(page, html, 1);
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*background:transparent;[^}]*color:#0a0a0a;", plain);
        Assert.Contains(":hover{background:#f5f5f5!important;color:#737373!important;}", plain, StringComparison.Ordinal);
        Assert.Contains(":has(>input:checked){background:#f5f5f5!important;color:#171717!important;}", plain, StringComparison.Ordinal);
        Assert.Contains(":hover{background:#f5f5f5!important;color:#262626!important;}", outline, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-90")]
    public async Task ThreeStateCheckBoxesDrawTheirThemesMarkAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{CheckTheme.Replace("</ControlTheme>", """
                <Style Selector="^:indeterminate /template/ Border#PART_Border"><Setter Property="Background" Value="#525252" /></Style>
                </ControlTheme>
                """, StringComparison.Ordinal)}}</StackPanel.Resources>
              <CheckBox Theme="{StaticResource Check}" IsThreeState="True" Content="All" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var label = Regex.Match(html, "<label class=\"u3 (mk[0-9a-f]+)").Groups[1].Value;
        Assert.Matches($"\\.{label}>\\.box\\{{appearance:none;[^}}]*width:16px;height:16px;[^}}]*border-radius:4px 4px 4px 4px;", page.Css);
        Assert.Matches($"\\.{label}\\.c3>\\.box\\{{background-color:#171717;", page.Css);
        Assert.Matches($"\\.{label}\\.i3>\\.box\\{{background-color:#525252;", page.Css);
    }

    private const string Dot = """<Ellipse Name="PART_Dot" Width="8" Height="8" IsVisible="False" Fill="#fafafa" />""";

    private const string Tick = """<Path Name="PART_Check" Width="14" Height="14" Stretch="None" IsVisible="False" Data="M11.667,3.5 L5.25,9.917 L2.333,7" StrokeThickness="1.5" Stroke="#fafafa" />""";

    /// <summary>A card theme (Aioros.Cosmo's questionnaire choice): a named root border around the indicator, the content and a description, and a shortcut.</summary>
    private static string Card(string target, string radius, string glyph, string shown) => $$"""
        <ControlTheme x:Key="Card" TargetType="{{target}}">
          <Setter Property="Background" Value="Transparent" />
          <Setter Property="BorderBrush" Value="#e5e5e5" />
          <Setter Property="BorderThickness" Value="1" />
          <Setter Property="CornerRadius" Value="18" />
          <Setter Property="Padding" Value="16,12" />
          <Setter Property="FocusAdorner" Value="{x:Null}" />
          <Setter Property="Template">
            <ControlTemplate>
              <Border Name="PART_Card" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                <Grid ColumnDefinitions="Auto,*,Auto">
                  <Border Name="PART_Indicator" Width="16" Height="16" CornerRadius="{{radius}}" Margin="0,0,12,0" VerticalAlignment="Top" Background="#e5e5e5">
                    {{glyph}}
                  </Border>
                  <StackPanel Grid.Column="1" Spacing="4">
                    <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                    <TextBlock Name="PART_Description" Text="{TemplateBinding Tag}" Foreground="#737373" />
                  </StackPanel>
                  <Border Name="PART_Shortcut" Grid.Column="2" Width="20" Height="20" CornerRadius="10" Margin="12,0,0,0" VerticalAlignment="Top" BorderBrush="#e5e5e5" BorderThickness="1">
                    <TextBlock Text="A" FontSize="10" HorizontalAlignment="Center" VerticalAlignment="Center" />
                  </Border>
                </Grid>
              </Border>
            </ControlTemplate>
          </Setter>
          <Style Selector="^:pointerover"><Setter Property="Background" Value="#fafafa" /></Style>
          <Style Selector="^:checked">
            <Setter Property="Background" Value="#f0f0f0" />
            <Setter Property="BorderBrush" Value="#171717" />
          </Style>
          <Style Selector="^:checked /template/ Border#PART_Indicator"><Setter Property="Background" Value="#171717" /></Style>
          <Style Selector="^:checked /template/ {{shown}}"><Setter Property="IsVisible" Value="True" /></Style>
        </ControlTheme>
        """;

    [Fact]
    [Trait("AC", "BUTTONS-114")]
    public async Task CardRadioButtonsHoldTheirTemplatesPartsAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{Card("RadioButton", "8", Dot, "Ellipse#PART_Dot")}}</StackPanel.Resources>
              <RadioButton Theme="{StaticResource Card}" Content="Tool calls" Tag="Show what ran." IsChecked="True" />
              <RadioButton Theme="{StaticResource Card}" Content="Approvals" Tag="Ask first." />
              <RadioButton Theme="{StaticResource Card}" GroupName="off" Content="Handoffs" Tag="Not now." IsChecked="True" IsEnabled="False" />
              <RadioButton Content="Fluent" />
              <RadioButton GroupName="plain" Content="Plain">
                <RadioButton.Theme>
                  <ControlTheme TargetType="RadioButton">
                    <Setter Property="Template">
                      <ControlTemplate>
                        <Border Name="RootBorder" Background="Transparent">
                          <Grid ColumnDefinitions="Auto,*">
                            <Border Name="PART_Mark" Width="16" Height="16" CornerRadius="8" BorderBrush="#e5e5e5" BorderThickness="1" />
                            <ContentPresenter Name="PART_ContentPresenter" Grid.Column="1" Margin="8,0,0,0" Content="{TemplateBinding Content}" />
                          </Grid>
                        </Border>
                      </ControlTemplate>
                    </Setter>
                  </ControlTheme>
                </RadioButton.Theme>
              </RadioButton>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<label class=\"[^\"]*mk[0-9a-f]+[^\"]*\"><input type=\"radio\" name=\"f[0-9]+\"(?: data-aioros-local=\"\")? value=\"0\">Plain</label>", html);
        var rules = CardRules(page, html, 0);
        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("\\{[^}]*border-color:#e5e5e5;[^}]*border-radius:18px 18px 18px 18px;[^}]*padding:12px 16px 12px 16px;", rules);
        Assert.Matches(":has\\(input:checked\\)\\{[^}]*background(-color)?:#f0f0f0[^}]*border-color:#171717", rules);
        Assert.Matches(":hover\\{[^}]*background(-color)?:#fafafa", rules);
        Assert.Matches(" input\\{appearance:none;[^}]*width:16px;height:16px;[^}]*background-color:#e5e5e5;", rules);
        Assert.Matches(":has\\(input:checked\\) input\\{[^}]*background-color:#171717;[^}]*background-image:url\\(\"data:image/svg\\+xml,[^\"]*fill='%23fafafa'", rules);
        Assert.Matches("<label [^>]*>(?:(?!</label>).)*<input type=\"radio\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"0\" checked style=\"[^\"]*width:16px;height:16px;[^\"]*\">(?:(?!</label>).)*Tool calls(?:(?!</label>).)*Show what ran\\.(?:(?!</label>).)*>A<(?:(?!</label>).)*</label>", html);
        Assert.Matches("<input type=\"radio\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"1\" style=\"[^\"]*\">(?:(?!</label>).)*Approvals", html);
        Assert.Matches("\\{[^}]*background(-color)?:#f0f0f0;[^}]*border-color:#171717;", CardRules(page, html, 2));
        Assert.Matches("<label class=\"[^\"]+\"><input type=\"radio\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"2\">Fluent</label>", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-115")]
    public async Task CardCheckBoxesHoldTheirTemplatesPartsAsync()
    {
        var page = await fixture.CompileAsync($$"""
            <StackPanel>
              <StackPanel.Resources>{{Card("CheckBox", "5", Tick, "Path#PART_Check")}}</StackPanel.Resources>
              <CheckBox Theme="{StaticResource Card}" Content="Tests" Tag="Existing tests." IsChecked="True" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var rules = CardRules(page, html, 0);
        Assert.True(page.Reusable, page.Failure);
        Assert.Matches(":has\\(input:checked\\)\\{[^}]*border-color:#171717", rules);
        Assert.Matches(":has\\(input:checked\\) input\\{[^}]*background-color:#171717;[^}]*background-image:url\\(\"data:image/svg\\+xml,[^\"]*stroke='%23fafafa'", rules);
        Assert.Matches("<label [^>]*>(?:(?!</label>).)*<input type=\"checkbox\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"on\" checked style=\"[^\"]*\">(?:(?!</label>).)*Tests(?:(?!</label>).)*Existing tests\\.(?:(?!</label>).)*</label>", html);
    }

    /// <summary>The rules of a card's label: its own, its states' and those of the input it holds.</summary>
    private static string CardRules(CompiledPage page, string html, int index)
    {
        var classes = Regex.Matches(html, "<label[^>]* class=\"([^\"]+)\"")[index].Groups[1].Value.Split(' ');
        return string.Join('\n', page.Css.Split('\n').Where(rule => classes.Any(name => rule.StartsWith($".{name}", StringComparison.Ordinal) && rule.Length > name.Length + 1 && rule[name.Length + 1] is '{' or ':' or ' ')));
    }

    [Fact]
    [Trait("AC", "ENGINE-270")]
    public async Task AMarkLookIsReadForTheControlsOwnSizeAsync()
    {
        const string Wraps = """<CheckBox Theme="{StaticResource Check}" Width="120" Content="Checkbox should wrap its text" />""";
        const string Breaks = """<CheckBox Theme="{StaticResource Check}" Content="One&#10;Two" />""";
        async Task<List<string>> HeightsAsync(params string[] boxes)
        {
            var page = await fixture.CompileAsync($$"""
                <StackPanel>
                  <StackPanel.Resources>{{CheckTheme}}</StackPanel.Resources>
                  <StackPanel.Styles><Style Selector="CheckBox /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="TextWrapping" Value="Wrap" /></Style></StackPanel.Styles>
                  {{string.Concat(boxes)}}
                </StackPanel>
                """);
            var html = EngineFixture.Body(page);
            return [.. Enumerable.Range(0, boxes.Length).Select(index => Regex.Match(Rules(page, html, index), "min-height:([0-9.]+)px").Groups[1].Value)];
        }

        var together = await HeightsAsync("""<CheckBox Theme="{StaticResource Check}" Content="Accept" />""", Wraps, Breaks);
        var alone = (await HeightsAsync(Wraps)).Concat(await HeightsAsync(Breaks)).ToList();

        Assert.Equal(alone, together.Skip(1));
        Assert.NotEqual(together[0], together[1]);
        Assert.NotEqual(together[0], together[2]);
    }

    [Fact]
    [Trait("AC", "ENGINE-275")]
    public async Task AMarksRestIsTheUncheckedBoxAsync()
    {
        async Task<(string Mark, string Rules)> MarkAsync(string state)
        {
            var page = await fixture.CompileAsync($$"""<Border Name="Engine275"><StackPanel><CheckBox IsChecked="{{state}}" Content="All" /></StackPanel></Border>""");
            var html = EngineFixture.Body(page);
            var mark = Regex.Match(html, "<label[^>]* class=\"(mk[0-9a-f]+)").Groups[1].Value;
            return (mark, string.Join('\n', page.Css.Split('\n').Where(rule => rule.StartsWith($".{mark}", StringComparison.Ordinal))));
        }

        var indeterminate = await MarkAsync("{x:Null}");
        var clear = await MarkAsync("False");
        var rest = string.Join('\n', clear.Rules.Split('\n').Where(rule => !rule.Contains("checked", StringComparison.Ordinal)));
        var checkedRule = string.Join('\n', clear.Rules.Split('\n').Where(rule => rule.Contains("checked", StringComparison.Ordinal)));

        Assert.Equal(clear.Mark, indeterminate.Mark);
        Assert.DoesNotContain("url(", rest, StringComparison.Ordinal);
        Assert.Contains("url(", checkedRule, StringComparison.Ordinal);
    }

    private static string Rules(CompiledPage page, string html, int index)
    {
        var classes = Regex.Matches(html, "<label[^>]* class=\"([^\"]+)\"")[index].Groups[1].Value.Split(' ');
        return string.Join('\n', page.Css.Split('\n').Where(rule => classes.Any(name => rule.StartsWith($".{name}{{", StringComparison.Ordinal) || rule.StartsWith($".{name}:", StringComparison.Ordinal) || rule.StartsWith($".{name}>", StringComparison.Ordinal))));
    }
}
