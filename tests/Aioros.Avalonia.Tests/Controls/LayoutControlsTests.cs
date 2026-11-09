using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class LayoutControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "LAYOUT-01")]
    public async Task StackPanelsAreFlexAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Orientation="Horizontal" Spacing="12">
              <TextBlock Text="a" />
              <TextBlock Text="b" />
            </StackPanel>
            """);

        Assert.Contains("flex-direction:row;gap:12px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-02")]
    public async Task GridsAreCssGridsAsync()
    {
        var page = await fixture.CompileAsync("""
            <Grid ColumnDefinitions="200,*,2*,Auto" RowDefinitions="Auto,*" ColumnSpacing="8">
              <TextBlock Grid.Column="1" Grid.Row="1" Grid.ColumnSpan="2" Text="cell" />
            </Grid>
            """);

        Assert.Contains("display:grid;min-width:0;grid-template-columns:200px minmax(0,1fr) minmax(0,2fr) auto;grid-template-rows:auto minmax(0,1fr);column-gap:8px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-column:2 / span 2;grid-row:2 / span 1;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-03")]
    public async Task WrapPanelsWrapAndUniformGridsShareColumnsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <WrapPanel ItemSpacing="4" LineSpacing="6"><TextBlock Text="a" /><TextBlock Text="b" /></WrapPanel>
              <UniformGrid Columns="3"><TextBlock Text="a" /><TextBlock Text="b" /></UniformGrid>
            </StackPanel>
            """);

        Assert.Contains("flex-direction:row;flex-wrap:wrap;column-gap:4px;row-gap:6px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:repeat(3,minmax(0,1fr));", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-04")]
    public async Task BordersBecomeBoxesAsync()
    {
        var page = await fixture.CompileAsync("""
            <Border Background="#102030" BorderBrush="#ff0000" BorderThickness="1,2,3,4" CornerRadius="8" Padding="10" BoxShadow="0 4 8 0 #40000000">
              <TextBlock Text="boxed" />
            </Border>
            """);

        Assert.Contains("background:#102030;border-color:#ff0000;border-width:2px 3px 4px 1px;border-style:solid;border-radius:8px 8px 8px 8px;padding:10px 10px 10px 10px;box-shadow:0px 4px 8px 0px rgba(0,0,0,0.251);", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-05")]
    public async Task HorizontalScrollViewersScrollAsync()
    {
        var page = await fixture.CompileAsync("""
            <ScrollViewer HorizontalScrollBarVisibility="Auto">
              <StackPanel Orientation="Horizontal"><TextBlock Text="wide" /></StackPanel>
            </ScrollViewer>
            """);

        Assert.Contains("overflow-x:auto;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-06")]
    public async Task PlainWrappersAddNoElementAsync()
    {
        var html = await fixture.HtmlAsync("""
            <ContentControl>
              <Decorator>
                <UserControl><TextBlock Text="inside" /></UserControl>
              </Decorator>
            </ContentControl>
            """);

        Assert.Matches("^<div class=\"k[0-9a-f]+\">inside</div>$", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-07")]
    public async Task PanelsOverlayTheirChildrenAsync()
    {
        var page = await fixture.CompileAsync("""
            <Panel>
              <Border Background="Red" />
              <TextBlock Text="over" HorizontalAlignment="Right" VerticalAlignment="Bottom" />
            </Panel>
            """);

        Assert.Contains("display:grid;min-width:0;grid-template:minmax(0,1fr)/minmax(0,1fr);", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:1/1;background:#ff0000;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:1/1;justify-self:end;align-self:end;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-08")]
    public async Task DockPanelsDockInOrderAroundTheLastChildAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <DockPanel HorizontalSpacing="4" VerticalSpacing="2">
                <TextBlock DockPanel.Dock="Top" Text="top" />
                <TextBlock DockPanel.Dock="Left" Text="left" />
                <TextBlock DockPanel.Dock="Right" Text="right" />
                <TextBlock DockPanel.Dock="Bottom" Text="bottom" />
                <TextBlock Text="fill" />
              </DockPanel>
              <DockPanel LastChildFill="False">
                <TextBlock DockPanel.Dock="Right" Text="end" />
              </DockPanel>
            </StackPanel>
            """);

        Assert.Contains("display:grid;min-width:0;grid-template-columns:auto minmax(0,1fr) auto;grid-template-rows:auto minmax(0,1fr) auto;column-gap:4px;row-gap:2px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:1/1/2/4;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:2/1/4/2;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:2/3/4/4;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:3/2/4/3;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:2/2/3/3;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:minmax(0,1fr) auto;grid-template-rows:minmax(0,1fr);", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-area:1/2/2/3;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-09")]
    public async Task RelativePanelsPositionWithAnchorsAsync()
    {
        var page = await fixture.CompileAsync("""
            <RelativePanel Width="200">
              <Border Name="A" Width="50" Height="20" Background="Red" />
              <Border Width="40" Height="20" Background="Blue" RelativePanel.RightOf="A" RelativePanel.AlignTopWith="A" />
              <Border Width="30" Height="10" Background="Green" RelativePanel.Below="A" RelativePanel.AlignRightWithPanel="True" />
              <Border Width="10" Height="10" Background="Gray" RelativePanel.AlignHorizontalCenterWithPanel="True" RelativePanel.AlignBottomWithPanel="True" />
              <Border Name="C" Background="Yellow" RelativePanel.AlignHorizontalCenterWithPanel="True" RelativePanel.AlignVerticalCenterWithPanel="True"><TextBlock Text="Center" /></Border>
              <Border Width="10" Height="10" Background="Purple" RelativePanel.Above="C" RelativePanel.AlignRightWith="C" />
            </RelativePanel>
            """);

        Assert.Contains("position:relative;anchor-scope:all;min-height:44px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("position:absolute;anchor-name:--A;left:0;top:0;background:#ff0000;", page.Css, StringComparison.Ordinal);
        Assert.Contains("position:absolute;left:anchor(--A right);top:anchor(--A top);background:#0000ff;", page.Css, StringComparison.Ordinal);
        Assert.Contains("position:absolute;right:0;top:anchor(--A bottom);background:#008000;", page.Css, StringComparison.Ordinal);
        Assert.Contains("position:absolute;left:0;right:0;margin-inline:auto;bottom:0;background:#808080;", page.Css, StringComparison.Ordinal);
        Assert.Contains("position:absolute;anchor-name:--C;left:0;right:0;margin-inline:auto;width:fit-content;top:0;bottom:0;margin-block:auto;height:fit-content;background:#ffff00;", page.Css, StringComparison.Ordinal);
        Assert.Contains("position:absolute;right:anchor(--C right);bottom:anchor(--C top);background:#800080;", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("translate", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-10")]
    public async Task ReversibleStackPanelsReverseTheirFlowAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ReversibleStackPanel ReverseOrder="True" Spacing="4"><TextBlock Text="a" /><TextBlock Text="b" /></ReversibleStackPanel>
              <ReversibleStackPanel ReverseOrder="True" Orientation="Horizontal"><TextBlock Text="c" /><TextBlock Text="d" /></ReversibleStackPanel>
            </StackPanel>
            """);

        Assert.Contains("flex-direction:column-reverse;justify-content:flex-end;gap:4px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("flex-direction:row-reverse;justify-content:flex-end;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-11")]
    public async Task GridSplittersAreFocusableSeparatorsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Grid ColumnDefinitions="*,Auto,*" Width="400">
                <TextBlock Text="a" />
                <GridSplitter Grid.Column="1" />
                <TextBlock Grid.Column="2" Text="b" />
              </Grid>
              <Grid RowDefinitions="*,Auto,*" Height="200">
                <TextBlock Text="c" />
                <GridSplitter Grid.Row="1" ResizeDirection="Rows" />
                <TextBlock Grid.Row="2" Text="d" />
              </Grid>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        var splitter = Assert.Single(Regex.Matches(html, "<div role=\"separator\" tabindex=\"0\" aria-orientation=\"vertical\" aria-valuenow=\"50\" aria-valuemin=\"0\" aria-valuemax=\"100\" data-aioros-split=\"c 0 2\" data-aioros-tracks=\"s a s\" data-aioros-step=\"10px\" class=\"(k[0-9a-f]+)\"><input type=\"hidden\" name=\"f0\" value=\"50\"></div>"));
        Assert.Contains($".{splitter.Groups[1].Value}{{min-width:6px;grid-column:2 / span 1;grid-row:1 / span 1;min-height:6px;background:rgba(255,255,255,0.4);touch-action:none;cursor:col-resize;}}", page.Css, StringComparison.Ordinal);
        Assert.Matches("<div role=\"separator\" tabindex=\"0\" aria-orientation=\"horizontal\" aria-valuenow=\"50\" aria-valuemin=\"0\" aria-valuemax=\"100\" data-aioros-split=\"r 0 2\" data-aioros-tracks=\"s a s\" data-aioros-step=\"10px\" class=\"k[0-9a-f]+\"><input type=\"hidden\" name=\"f1\" value=\"50\"></div>", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-88")]
    public async Task AScrollViewersMaxHeightCapsItsBoxAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ScrollViewer Width="201" MaxHeight="120"><Border Height="300" /></ScrollViewer>
              <ScrollViewer Width="202" MaxHeight="120">
                <ScrollViewer.Theme>
                  <ControlTheme TargetType="ScrollViewer">
                    <Setter Property="Template">
                      <ControlTemplate>
                        <Border Background="White"><ScrollContentPresenter Name="PART_ContentPresenter" /></Border>
                      </ControlTemplate>
                    </Setter>
                  </ControlTheme>
                </ScrollViewer.Theme>
                <Border Height="300" />
              </ScrollViewer>
              <ScrollViewer Width="203"><Border Height="300" /></ScrollViewer>
            </StackPanel>
            """);

        string Rule(int width) => page.Css.Split('\n').Single(rule => rule.Contains($"width:{width}px;", StringComparison.Ordinal));
        Assert.Equal((true, true, false), (Rule(201).Contains("max-height:120px;", StringComparison.Ordinal), Rule(202).Contains("max-height:120px;", StringComparison.Ordinal), Rule(203).Contains("max-height", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("AC", "LAYOUT-86")]
    public async Task AGridSplittersPositionTravelsAndIsAppliedAsADragAsync()
    {
        const string Body = """
            <StackPanel>
              <Grid Width="401" Height="100" HorizontalAlignment="Left" ColumnDefinitions="25*,Auto,75*">
                <TextBlock Text="a" />
                <m:SyncedSplitter Grid.Column="1" Width="1" a:Web.Delay="150" />
                <TextBlock Grid.Column="2" Text="b" />
              </Grid>
              <Grid Width="401" Height="100" HorizontalAlignment="Left" ColumnDefinitions="25*,Auto,75*">
                <TextBlock Text="c" />
                <GridSplitter Grid.Column="1" Width="1" />
                <TextBlock Grid.Column="2" Text="d" />
              </Grid>
            </StackPanel>
            """;
        var html = await fixture.HtmlAsync(Body, new Splitting());
        var moved = new Splitting();
        await fixture.PostAsync(Body, moved, ("f0", "40"), ("f1", "25"));
        var still = new Splitting();
        await fixture.PostAsync(Body, still, ("f0", "25"), ("f1", "25"));

        Assert.Matches("<div role=\"separator\"[^>]* data-aioros-split=\"c 0 2\"[^>]*><input type=\"hidden\" name=\"f0\" value=\"25\" data-change data-delay=\"150\"></div>", html);
        Assert.Matches("<div role=\"separator\"[^>]* data-aioros-split=\"c 0 2\"[^>]*><input type=\"hidden\" name=\"f1\" value=\"25\"></div>", html);
        Assert.Equal((Math.Round(moved.Stars * 0.4, 1), 1), (Math.Round(moved.Seen, 1), moved.Completed));
        Assert.True(moved.Stars > 300, $"{moved.Stars}");
        Assert.Equal(0, still.Completed);
    }

    [Fact]
    [Trait("AC", "LAYOUT-59")]
    public async Task ThemedGridSplittersWriteTheirTemplateAndLimitsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <UserControl.Resources>
              <ControlTheme x:Key="Grip" TargetType="GridSplitter">
                <Setter Property="Background" Value="Gray" />
                <Setter Property="Width" Value="1" />
                <Setter Property="MinWidth" Value="1" />
                <Setter Property="a:Web.ResizeStep" Value="5" />
                <Setter Property="a:Web.CollapsedSize" Value="0" />
                <Setter Property="Template">
                  <ControlTemplate>
                    <Panel>
                      <Border Name="PART_Line" Background="{TemplateBinding Background}" />
                      <Border Name="PART_Grip" Width="12" Height="16" CornerRadius="2" Background="Silver" HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Panel>
                  </ControlTemplate>
                </Setter>
              </ControlTheme>
            </UserControl.Resources>
            <Grid Width="401" Height="100" HorizontalAlignment="Left">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="25*" MinWidth="40" a:Web.MinSize="10" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="75*" a:Web.MinSize="25" />
              </Grid.ColumnDefinitions>
              <TextBlock Text="a" />
              <GridSplitter Grid.Column="1" Theme="{StaticResource Grip}" />
              <TextBlock Grid.Column="2" Text="b" />
            </Grid>
            """);

        Assert.Matches("<div role=\"separator\" tabindex=\"0\" aria-orientation=\"vertical\" aria-valuenow=\"25\" aria-valuemin=\"10\" aria-valuemax=\"75\" data-aioros-split=\"c 0 2\" data-aioros-tracks=\"s a s\" data-aioros-step=\"5%\" data-aioros-collapse=\"0\" data-aioros-px=\"40 Infinity 0 Infinity\" class=\"k[0-9a-f]+\"><input type=\"hidden\" name=\"f0\" value=\"25\"><div[^>]*>(<div[^>]*></div>){2}</div></div>", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-12")]
    public async Task RenderTransformsBecomeCssTransformsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="turned" RenderTransform="rotate(90deg)" />
              <Border Width="10" Height="10" Background="Red" RenderTransformOrigin="0,0">
                <Border.RenderTransform>
                  <TransformGroup><ScaleTransform ScaleX="2" ScaleY="3" /><TranslateTransform X="5" Y="6" /></TransformGroup>
                </Border.RenderTransform>
              </Border>
              <Border Width="10" Height="10" Background="Blue" RenderTransformOrigin="25%,100%" RenderTransform="skewX(45deg)" />
            </StackPanel>
            """);

        Assert.Contains("transform:matrix(0,1,-1,0,0,0);", page.Css, StringComparison.Ordinal);
        Assert.Contains("transform:matrix(2,0,0,3,5,6);transform-origin:0px 0px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("transform:matrix(1,0,1,1,0,0);transform-origin:25% 100%;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-13")]
    public async Task LayoutTransformsTakeTheTransformedSizeAsync()
    {
        var page = await fixture.CompileAsync("""
            <LayoutTransformControl HorizontalAlignment="Left">
              <LayoutTransformControl.LayoutTransform><RotateTransform Angle="90" /></LayoutTransformControl.LayoutTransform>
              <Border Width="40" Height="10" Background="Red" />
            </LayoutTransformControl>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("^<div class=\"k[0-9a-f]+\"><div class=\"k[0-9a-f]+\"><div class=\"c k[0-9a-f]+\"></div></div></div>$", html);
        Assert.Contains("align-self:flex-start;flex:1 1 0%;min-height:0;width:10px;height:40px;display:grid;place-items:center;", page.Css, StringComparison.Ordinal);
        Assert.Contains("{transform:matrix(0,1,-1,0,0,0);}", page.Css, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(page.Css, "matrix\\("));
    }

    [Fact]
    [Trait("AC", "LAYOUT-14")]
    public async Task SeparatorsAreRulesAsync()
    {
        var page = await fixture.CompileAsync("<Separator />");
        var html = EngineFixture.Body(page, [null]);

        var rule = Assert.Single(Regex.Matches(html, "^<hr class=\"(k[0-9a-f]+)\">$"));
        Assert.Contains($".{rule.Groups[1].Value}{{margin:4px 12px 4px 12px;height:1px;border:0;flex-shrink:0;background:rgba(0,0,0,0.4);}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-15")]
    public async Task GroupBoxesAreFieldsetsAsync()
    {
        var page = await fixture.CompileAsync("""
            <GroupBox Header="Shipping"><TextBlock Text="Inside" /></GroupBox>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("^<fieldset class=\"k[0-9a-f]+\"><legend class=\"k[0-9a-f]+\"><div class=\"k[0-9a-f]+\">Shipping</div></legend><div class=\"k[0-9a-f]+\">Inside</div></fieldset>$", html);
        Assert.Contains("margin:0;min-width:0;display:flex;flex-direction:column;border-color:rgba(0,0,0,0.6);border-width:1px 1px 1px 1px;border-style:solid;border-radius:3px 3px 3px 3px;padding:4px 9px 9px 9px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("{padding:0 3px;margin-left:-4px;}", page.Css, StringComparison.Ordinal);
        Assert.Contains("font-size:16px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-16")]
    public async Task HeaderedContentControlsWriteTheirTemplateAsync()
    {
        var page = await fixture.CompileAsync("""
            <HeaderedContentControl Header="Head" Content="Body" />
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("^<div class=\"c k[0-9a-f]+\"><div class=\"k[0-9a-f]+\"><div class=\"c k[0-9a-f]+\"><div class=\"k[0-9a-f]+\">Head</div></div><div class=\"c k[0-9a-f]+\"><div class=\"k[0-9a-f]+\">Body</div></div></div></div>$", html);
        Assert.Contains("padding:3px 3px 3px 3px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-template-rows:auto minmax(0,1fr);", page.Css, StringComparison.Ordinal);
        Assert.Contains("grid-column:1 / span 1;grid-row:2 / span 1;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-17")]
    public async Task ThemeVariantScopesResolveTheirVariantAsync()
    {
        var page = await fixture.CompileAsync("""
            <ThemeVariantScope RequestedThemeVariant="Dark">
              <Border Background="{DynamicResource SystemControlBackgroundAltHighBrush}" Padding="8"><TextBlock Text="dark" /></Border>
            </ThemeVariantScope>
            """);

        Assert.Contains("color-scheme:dark;color:#ffffff;", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:#000000;padding:8px 8px 8px 8px;", page.Css, StringComparison.Ordinal);
        Assert.Contains(":root{color-scheme:light}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-18")]
    public async Task AcrylicBordersBlurTheBackdropAsync()
    {
        var page = await fixture.CompileAsync("""
            <ExperimentalAcrylicBorder CornerRadius="8">
              <ExperimentalAcrylicBorder.Material>
                <ExperimentalAcrylicMaterial TintColor="Blue" TintOpacity="0.5" MaterialOpacity="0.6" />
              </ExperimentalAcrylicBorder.Material>
              <TextBlock Text="acrylic" />
            </ExperimentalAcrylicBorder>
            """);

        Assert.Contains("background:linear-gradient(rgba(0,0,255,0.224),rgba(0,0,255,0.224)),#a5a5a5;backdrop-filter:blur(30px);border-radius:8px 8px 8px 8px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-19")]
    public async Task VirtualizingPanelsWriteTheirItemsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ItemsControl>
                <ItemsControl.ItemsPanel><ItemsPanelTemplate><VirtualizingStackPanel Orientation="Horizontal" /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
                <TextBlock Text="one" /><TextBlock Text="two" /><TextBlock Text="three" />
              </ItemsControl>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches(">one</div></div><div role=\"listitem\" class=\"li\"><div class=\"k[0-9a-f]+\">two</div></div><div role=\"listitem\" class=\"li\"><div class=\"k[0-9a-f]+\">three<", html);
        Assert.Contains("flex-direction:row;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-20")]
    public async Task TemplatedControlsWriteTheirTemplateAsync()
    {
        var page = await fixture.CompileAsync("""
            <UserControl.Resources>
              <ControlTheme x:Key="{x:Type m:Badge}" TargetType="m:Badge">
                <Setter Property="Template">
                  <ControlTemplate>
                    <Border Background="{TemplateBinding Background}" CornerRadius="9" Padding="6,2"><TextBlock Text="{TemplateBinding Label}" /></Border>
                  </ControlTemplate>
                </Setter>
              </ControlTheme>
            </UserControl.Resources>
            <m:Badge Label="New" Background="#ffd700" HorizontalAlignment="Left" />
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("^<div class=\"c k[0-9a-f]+\"><div class=\"c k[0-9a-f]+\"><div class=\"k[0-9a-f]+\">New</div></div></div>$", html);
        Assert.Contains("background:#ffd700;border-radius:9px 9px 9px 9px;padding:2px 6px 2px 6px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-21")]
    public async Task GradientBrushesBecomeCssGradientsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Height="20">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
              </Border>
              <Border Height="20">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0%,25%" EndPoint="0%,75%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
              </Border>
              <Border Height="20">
                <Border.Background>
                  <RadialGradientBrush Center="50%,25%" RadiusX="40%" RadiusY="60%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></RadialGradientBrush>
                </Border.Background>
              </Border>
              <Border Height="20">
                <Border.Background>
                  <ConicGradientBrush Angle="90" Center="50%,50%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="0.5" Color="#800000ff" /></ConicGradientBrush>
                </Border.Background>
              </Border>
            </StackPanel>
            """);

        Assert.Contains("background:linear-gradient(90deg,#ff0000 0%,#0000ff 100%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(180deg,#ff0000 25%,#0000ff 75%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:radial-gradient(ellipse 40% 60% at 50% 25%,#ff0000 0%,#0000ff 100%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:conic-gradient(from 90deg at 50% 50%,#ff0000 0%,rgba(0,0,255,0.502) 50%);", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-22")]
    public async Task OpacityClippingAndStackingAreCssAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Height="10" Background="Red" Opacity="0.5" ClipToBounds="True" ZIndex="2" />
            </StackPanel>
            """);

        Assert.Contains("height:10px;opacity:0.5;overflow:hidden;z-index:2;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-23")]
    public async Task SelfDrawnControlsAreSvgOfTheirRenderAsync()
    {
        var html = await fixture.HtmlAsync("""
            <m:HalfSwatch Width="4" Height="2" HorizontalAlignment="Left" />
            """);

        Assert.Matches("^<svg width=\"4\" height=\"2\" viewBox=\"0 0 4 2\" aria-hidden=\"true\" class=\"k[0-9a-f]+\"><path d=\"M0 0[^\"]*\" fill=\"#ff0000\"/></svg>$", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-25")]
    public async Task SelfDrawnStrokesTransformsAndTextAreSvgAsync()
    {
        var html = await fixture.HtmlAsync("""
            <m:DashedLabel Width="40" Height="30" AutomationProperties.Name="Dashes" HorizontalAlignment="Left" />
            """);

        Assert.StartsWith("<svg width=\"40\" height=\"30\" viewBox=\"0 0 40 30\" role=\"img\" aria-label=\"Dashes\"", html, StringComparison.Ordinal);
        Assert.Matches("<path d=\"M0 5[^\"]*\" fill=\"none\" stroke=\"#0000ff\" stroke-width=\"2\" stroke-dasharray=\"6 2\" stroke-linecap=\"round\" stroke-miterlimit=\"10\"/>", html);
        Assert.Matches("<g transform=\"matrix\\(1 0 0 1 4 10\\)\"><g opacity=\"0.5\"><path d=\"M[^\"]+\" fill=\"#008000\"/></g></g>", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-26")]
    public async Task UnrecordableDrawingsStayImagesOfTheirRenderAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <m:ConicSwatch Width="4" Height="2" HorizontalAlignment="Left" />
              <m:CustomDrawn Width="6" Height="3" HorizontalAlignment="Left" />
            </StackPanel>
            """);

        var images = Regex.Matches(html, "<img src=\"data:image/png;base64,([A-Za-z0-9+/=]+)\" alt=\"\" width=\"(\\d)\" height=\"(\\d)\" class=\"k[0-9a-f]+\">");
        Assert.Equal([(4, 2), (6, 3)], images.Select(image => Size(Convert.FromBase64String(image.Groups[1].Value))));
        Assert.DoesNotContain("<svg", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-37")]
    public async Task BitmapsAndRoundedClipsStayImagesOfTheirRenderAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <m:BitmapDrawn Width="4" Height="2" HorizontalAlignment="Left" />
              <m:RoundClipped Width="6" Height="3" HorizontalAlignment="Left" />
            </StackPanel>
            """);

        var images = Regex.Matches(html, "<img src=\"data:image/png;base64,([A-Za-z0-9+/=]+)\" alt=\"\" width=\"(\\d)\" height=\"(\\d)\" class=\"k[0-9a-f]+\">");
        Assert.Equal([(4, 2), (6, 3)], images.Select(image => Size(Convert.FromBase64String(image.Groups[1].Value))));
    }

    [Fact]
    [Trait("AC", "LAYOUT-38")]
    public async Task GreedyDrawingsAreMeasuredAgainstTheViewportAsync()
    {
        var html = await fixture.HtmlAsync("""
            <m:Greedy HorizontalAlignment="Left" />
            """);

        Assert.Matches("^<svg width=\"1280\" height=\"800\" viewBox=\"0 0 1280 800\" aria-hidden=\"true\" class=\"k[0-9a-f]+\"><path d=\"M0 0[^\"]*\" fill=\"#ff0000\"/></svg>$", html);
    }

    private static (int Width, int Height) Size(byte[] png) =>
        (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));

    [Fact]
    [Trait("AC", "LAYOUT-24")]
    public async Task SizedStretchedChildrenAreCenteredAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Width="40" Height="10" Background="Red" />
              <StackPanel Orientation="Horizontal"><Border Width="30" Height="20" Background="Green" /></StackPanel>
              <Grid ColumnDefinitions="*"><Border Width="50" Background="Blue" /></Grid>
            </StackPanel>
            """);

        Assert.Contains("width:40px;height:10px;align-self:center;background:#ff0000;", page.Css, StringComparison.Ordinal);
        Assert.Contains("width:30px;height:20px;align-self:center;flex-shrink:0;background:#008000;", page.Css, StringComparison.Ordinal);
        Assert.Contains("width:50px;grid-column:1 / span 1;grid-row:1 / span 1;justify-self:center;background:#0000ff;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-27")]
    public async Task BorderThicknessKeepsItsWidthWithoutABrushAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border BorderThickness="3" Width="20" Height="10" />
              <Button Content="Save" />
            </StackPanel>
            """);

        Assert.Contains("border-color:transparent;border-width:3px 3px 3px 3px;border-style:solid;", page.Css, StringComparison.Ordinal);
        Assert.Contains("border-color:transparent;border-width:1px 1px 1px 1px;border-style:solid;", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("border-width:0;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-48")]
    public async Task ControlsCarryTheirIdNameAndResponsiveClassesAsync()
    {
        const string Hints = "a:Web.Id=\"pick\" AutomationProperties.Name=\"Pick\" a:Web.HideBelow=\"600\"";
        foreach (var (xaml, element) in new[]
        {
            ("<CheckBox HINTS>C</CheckBox>", "input"),
            ("<RadioButton HINTS>R</RadioButton>", "input"),
            ("<ToggleSwitch HINTS />", "input"),
            ("<ComboBox HINTS><ComboBoxItem>A</ComboBoxItem></ComboBox>", "select"),
            ("<Slider HINTS />", "input"),
            ("<ProgressBar HINTS Value=\"3\" />", "progress"),
            ("<DatePicker HINTS />", "input"),
            ("<ColorPicker HINTS />", "input"),
        })
        {
            var html = await fixture.HtmlAsync(xaml.Replace("HINTS", Hints, StringComparison.Ordinal));

            Assert.Matches("^<[a-z]+ [^>]*class=\"[^\"]*\\bhb600\\b", html);
            Assert.Matches($"<{element} [^>]*id=\"pick\" aria-label=\"Pick\"", html);
            Assert.Single(Regex.Matches(html, "id=\"pick\""));
        }
    }

    [Fact]
    [Trait("AC", "LAYOUT-49")]
    public async Task StackedGridsReachTheCellsOfWrappersWithoutABoxAsync()
    {
        var page = await fixture.CompileAsync("""
            <Grid ColumnDefinitions="*,*" a:Web.StackBelow="600">
              <TextBlock Text="a" />
              <TextBlock Grid.Column="1" Text="b" />
            </Grid>
            """);

        Assert.Contains("@media (max-width:600px){.sb600{grid-template-columns:minmax(0,1fr)!important;grid-auto-flow:row!important}.sb600>*,.sb600>[style*=\"display:contents\"]>*,.sb600>[style*=\"display:contents\"]>[style*=\"display:contents\"]>*{grid-column:1/-1!important;grid-row:auto!important}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-97")]
    public async Task StackedUniformGridsFlowTheirItemsInOneColumnAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ItemsControl>
                <ItemsControl.ItemsPanel><ItemsPanelTemplate><UniformGrid Columns="4" a:Web.StackBelow="900" /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
                <x:String>a</x:String>
                <x:String>b</x:String>
              </ItemsControl>
              <UniformGrid Rows="1" a:Web.StackBelow="600">
                <TextBlock Text="c" />
                <TextBlock Text="d" />
              </UniformGrid>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);

        Assert.Contains("class=\"sb900 ", html, StringComparison.Ordinal);
        Assert.Contains("class=\"sb600 ", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:900px){.sb900{grid-template-columns:minmax(0,1fr)!important;grid-auto-flow:row!important}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-50")]
    public async Task CssGradientsArePlacedOnTheBoxLikeAvaloniaAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Width="200" Height="100" CornerRadius="1">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
              </Border>
              <Border Width="200" Height="100" CornerRadius="2">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0,0" EndPoint="50,0"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
              </Border>
              <Border Width="200" Height="100" CornerRadius="3">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="50%,0%" SpreadMethod="Reflect"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
              </Border>
              <Border Width="200" Height="100" CornerRadius="4">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="25%,0%" SpreadMethod="Repeat"><GradientStop Offset="0.25" Color="Red" /><GradientStop Offset="0.75" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
              </Border>
              <Border Width="200" Height="100" CornerRadius="5">
                <Border.Background>
                  <RadialGradientBrush SpreadMethod="Reflect"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></RadialGradientBrush>
                </Border.Background>
              </Border>
            </StackPanel>
            """);

        Assert.Contains("background:linear-gradient(116.565deg,#ff0000 0%,#0000ff 100%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(90deg,#ff0000 0%,#0000ff 25%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:repeating-linear-gradient(90deg,#ff0000 0%,#0000ff 50%,#ff0000 100%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:repeating-linear-gradient(90deg,#ff0000 0%,#ff0000 6.25%,#0000ff 18.75%,#0000ff 25%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:repeating-radial-gradient(ellipse 50% 50% at 50% 50%,#ff0000 0%,#0000ff 100%,#ff0000 200%);", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-51")]
    public async Task GradientBackgroundsOfPanelsAndPlainBordersAreKeptAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Grid Width="100" Height="20">
                <Grid.Background>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Lime" /></LinearGradientBrush>
                </Grid.Background>
                <TextBlock Text="Grid" />
              </Grid>
              <Border Width="100" Height="20">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.Background>
                <TextBlock Text="Border" />
              </Border>
            </StackPanel>
            """);

        Assert.Contains("background:linear-gradient(90deg,#ff0000 0%,#00ff00 100%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(90deg,#ff0000 0%,#0000ff 100%);", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-52")]
    public async Task GradientBorderBrushesAndStrokesAreCssGradientsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Width="100" Height="20" BorderThickness="2" CornerRadius="4" Background="White">
                <Border.BorderBrush>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.BorderBrush>
              </Border>
              <Border Width="100" Height="20" BorderThickness="3">
                <Border.BorderBrush>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="0%,100%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Border.BorderBrush>
              </Border>
              <Rectangle Height="20" StrokeThickness="2" Fill="White">
                <Rectangle.Stroke>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Lime" /></LinearGradientBrush>
                </Rectangle.Stroke>
              </Rectangle>
            </StackPanel>
            """);

        Assert.Contains("background:linear-gradient(#ffffff,#ffffff) padding-box,linear-gradient(90deg,#ff0000 0%,#0000ff 100%) border-box;border-color:transparent;border-width:2px 2px 2px 2px;border-style:solid;", page.Css, StringComparison.Ordinal);
        Assert.Contains("border-color:transparent;border-width:3px 3px 3px 3px;border-style:solid;border-image:linear-gradient(180deg,#ff0000 0%,#0000ff 100%) 1;", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(#ffffff,#ffffff) padding-box,linear-gradient(90deg,#ff0000 0%,#00ff00 100%) border-box;border:2px solid transparent;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-53")]
    public async Task SelfDrawnGradientsAreSvgGradientsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <m:GradientDrawn Width="40" Height="10" HorizontalAlignment="Left" />
            """);

        Assert.Matches("""^<svg width="40" height="10" viewBox="0 0 40 10" aria-hidden="true" class="k[0-9a-f]+"><defs><linearGradient id="(vg[0-9a-f]{8})" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="20" y2="10">(<stop[^>]+/>){2}</linearGradient><radialGradient id="(vg[0-9a-f]{8})" gradientUnits="userSpaceOnUse" cx="30" cy="5" r="5">(<stop[^>]+/>){2}</radialGradient></defs><path d="M0 0L20 0L20 10L0 10L0 0Z" fill="url\(#\1\)"/><path d="M[^"]+" fill="none" stroke="url\(#\3\)" stroke-width="2" stroke-miterlimit="10"/></svg>$""", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-54")]
    public async Task WindowFillingScrollViewersMakeAViewportShellAsync()
    {
        var shell = await fixture.CompileAsync("""
            <DockPanel>
              <Border DockPanel.Dock="Top" Padding="8"><TextBlock Text="Header" /></Border>
              <ScrollViewer DockPanel.Dock="Left" Width="200"><StackPanel><TextBlock Text="Nav" /></StackPanel></ScrollViewer>
              <ScrollViewer><StackPanel><TextBlock Text="Content" /></StackPanel></ScrollViewer>
            </DockPanel>
            """);
        var stacked = await fixture.CompileAsync("""
            <StackPanel><ScrollViewer><TextBlock Text="Content" /></ScrollViewer></StackPanel>
            """);

        var html = EngineFixture.Body(shell);
        Assert.Matches("<div class=\"[^\"]*\\bvf\\b[^\"]*\"[^>]*><div[^>]*>[^<]*<div[^>]*>Header", html);
        Assert.Equal(2, Regex.Count(html, "class=\"[^\"]*\\bvs\\b"));
        Assert.Contains("form:has(.vf){height:100dvh;min-height:0!important;overflow:hidden}", shell.Css, StringComparison.Ordinal);
        Assert.Contains(".vs{overflow:hidden auto!important}", shell.Css, StringComparison.Ordinal);
        Assert.Contains(".c>.vf{flex:1 1 0!important}", shell.Css, StringComparison.Ordinal);
        Assert.DoesNotMatch("\\bv[fs]\\b", EngineFixture.Body(stacked));
        Assert.DoesNotContain("100dvh", stacked.Css, StringComparison.Ordinal);
        Assert.Contains("querySelectorAll(\".vs,.vsx\")", System.Text.Encoding.UTF8.GetString(Aioros.Targets.Ssr.Documents.Scripts.Bytes.Span), StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "LAYOUT-96")]
    public async Task LibraryShellsAndOutletsPassTheViewportsHeightAsync()
    {
        static string Shell(string inset) => $$"""
            <m:Shell a:Web.Layout="Dock">
              <Border DockPanel.Dock="Left" Width="200"><TextBlock Text="Nav" /></Border>
              <m:Inset {{inset}}>
                <DockPanel>
                  <Border DockPanel.Dock="Top" Padding="8"><TextBlock Text="Header" /></Border>
                  <a:Outlet><Panel><ScrollViewer><StackPanel><TextBlock Text="Content" /></StackPanel></ScrollViewer></Panel></a:Outlet>
                </DockPanel>
              </m:Inset>
            </m:Shell>
            """;
        var filled = await fixture.CompileAsync(Shell("a:Web.Layout=\"Fill\""));
        var unfilled = await fixture.CompileAsync(Shell(string.Empty));

        var html = EngineFixture.Body(filled, null, null);
        Assert.Equal(1, Regex.Count(html, "class=\"[^\"]*\\bvs\\b"));
        Assert.Contains("form:has(.vf){height:100dvh;min-height:0!important;overflow:hidden}", filled.Css, StringComparison.Ordinal);
        Assert.True(Regex.Count(html, "class=\"[^\"]*\\bvf\\b") >= 5, html);
        Assert.DoesNotMatch("\\bv[fs]\\b", EngineFixture.Body(unfilled, null, null));
        Assert.DoesNotContain("100dvh", unfilled.Css, StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "LAYOUT-55")]
    public async Task ItemsInAGridPanelTakeTheirCellsAsync()
    {
        var page = await fixture.CompileAsync("""
            <ItemsControl>
              <ItemsControl.ItemsPanel><ItemsPanelTemplate><Grid ColumnDefinitions="Auto,*,Auto" /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
              <TextBlock Text="Start" Grid.Column="0" />
              <TextBlock Text="Middle" Grid.Column="1" />
              <TextBlock Text="End" Grid.Column="2" />
            </ItemsControl>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css.Split('\n').Single(rule => rule.StartsWith("." + Regex.Match(html, "class=\"(k[0-9a-f]+)\"[^>]*>" + text).Groups[1].Value + "{", StringComparison.Ordinal));
        Assert.Contains("grid-column:1 / span 1;", Rule("Start"), StringComparison.Ordinal);
        Assert.Contains("grid-column:2 / span 1;", Rule("Middle"), StringComparison.Ordinal);
        Assert.Contains("grid-column:3 / span 1;", Rule("End"), StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "LAYOUT-56")]
    public async Task ContentPresentersWriteTheirOwnBoxAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ContentControl Padding="12,6,0,6"><TextBlock Text="Padded" /></ContentControl>
              <ContentControl><TextBlock Text="Plain" /></ContentControl>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<div class=\"c (k[0-9a-f]+)\"><div class=\"k[0-9a-f]+\">Padded</div></div>", html);
        var padded = Regex.Match(html, "<div class=\"c (k[0-9a-f]+)\"><div class=\"k[0-9a-f]+\">Padded").Groups[1].Value;
        Assert.Contains("padding:6px 0px 6px 12px;", page.Css.Split('\n').Single(rule => rule.StartsWith("." + padded + "{", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Matches("<div class=\"k[0-9a-f]+\">Plain</div>", html);
    }

    [Fact]
    [Trait("AC", "LAYOUT-57")]
    public async Task RoundedTemplatePartsClipTheirContentAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Round" TargetType="ContentControl">
                  <Setter Property="CornerRadius" Value="16" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" CornerRadius="{TemplateBinding CornerRadius}" ClipToBounds="True" Width="32" Height="32">
                        <Image Source="avares://Aioros.Avalonia.Tests/Assets/dot.png" Stretch="UniformToFill" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Open" TargetType="ContentControl">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" CornerRadius="6" Width="20" Height="20" Background="Red" />
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <ContentControl Theme="{StaticResource Round}" />
              <ContentControl Theme="{StaticResource Open}" />
            </StackPanel>
            """);

        Assert.Matches("\\{width:32px;height:32px;overflow:hidden;[^}]*border-radius:16px 16px 16px 16px;", page.Css);
        Assert.DoesNotMatch("\\{width:20px;height:20px;[^}]*overflow:hidden", page.Css);
    }

    [Fact]
    [Trait("AC", "LAYOUT-58")]
    public async Task StretchedControlsKeepTheirMarginsWithinTheirMaxWidthAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border MaxWidth="512" Margin="16" Height="10" Background="Red" />
              <Border MaxWidth="200" Margin="8,0,24,0" Height="10" Background="Blue" />
            </StackPanel>
            """);

        Assert.Contains("margin:16px auto 16px auto;width:calc(100% - 32px);height:10px;max-width:512px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("margin:0px auto 0px max(8px,calc((100% - 216px) / 2));width:calc(100% - 32px);height:10px;max-width:200px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-60")]
    public async Task AThemedScrollViewerScrollsInsideItsFrameAsync()
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
                  <Setter Property="ClipToBounds" Value="True" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" ClipToBounds="True">
                        <Panel>
                          <ScrollContentPresenter Name="PART_ContentPresenter" Padding="{TemplateBinding Padding}" />
                          <ScrollBar Name="PART_HorizontalScrollBar" Orientation="Horizontal" Theme="{StaticResource Bar}" />
                          <ScrollBar Name="PART_VerticalScrollBar" Orientation="Vertical" Theme="{StaticResource Bar}" />
                        </Panel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <ScrollViewer Theme="{StaticResource Area}" Width="384" HorizontalAlignment="Left" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled" BorderThickness="1" BorderBrush="#e5e5e5" CornerRadius="6">
                <StackPanel Orientation="Horizontal" Spacing="16" Margin="16">
                  <Border Width="300" Height="40" Background="Red" />
                  <Border Width="300" Height="40" Background="Blue" />
                </StackPanel>
              </ScrollViewer>
              <ScrollViewer Height="40" HorizontalScrollBarVisibility="Auto"><TextBlock Text="Fluent" /></ScrollViewer>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var frame = Regex.Match(html, "<div class=\"c sxw (k[0-9a-f]+)\"><div class=\"c hs (k[0-9a-f]+)\"><div class=\"c k[0-9a-f]+\"></div>");
        string Rule(string name) => Regex.Match(page.Css, $"(?m)^\\.{name}\\{{[^}}]*\\}}").Value;

        Assert.True(frame.Success, html);
        Assert.Contains("width:384px;", Rule(frame.Groups[1].Value), StringComparison.Ordinal);
        Assert.Contains("border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-style:solid;border-radius:6px 6px 6px 6px;overflow-x:auto;overflow-y:hidden;scrollbar-width:thin;scrollbar-color:#e5e5e5 transparent;", Rule(frame.Groups[1].Value), StringComparison.Ordinal);
        Assert.Contains("flex-direction:row;gap:16px;", Rule(frame.Groups[2].Value), StringComparison.Ordinal);
        Assert.Contains(".sxw>*{min-width:max-content}", page.Css, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "class=\"c sxw "));
        Assert.Single(Regex.Matches(page.Css, "scrollbar-width:thin"));
        Assert.Contains(">Fluent</div>", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-66")]
    public async Task AStretchedChildOfAFixedSizeIsCenteredInItsBorderAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Width="32" Height="32" Background="#f5f5f5"><Viewbox Width="16" Height="16"><Rectangle Width="24" Height="24" Fill="#0a0a0a" /></Viewbox></Border>
              <Border Width="32" Height="32" Background="#f5f5f5"><Border Width="16" Height="16" Margin="4" Background="#0a0a0a" /></Border>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rule(string name) => Regex.Match(page.Css, $@"\.{name}\{{(?<rule>[^}}]*)\}}").Groups["rule"].Value;
        var viewbox = Regex.Match(html, "<svg[^>]* class=\"(?<class>[^\"]+)\"").Groups["class"].Value;
        var inner = Regex.Match(html, "<div class=\"c (?<class>k[0-9a-f]+)\"></div>").Groups["class"].Value;

        Assert.Contains("align-self:center;margin-top:auto;margin-bottom:auto;", Rule(viewbox), StringComparison.Ordinal);
        Assert.Contains("margin:4px 4px 4px 4px;", Rule(inner), StringComparison.Ordinal);
        Assert.DoesNotContain("margin-top:auto", Rule(inner), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-67")]
    public async Task AStretchedOnlyChildTakesItsParentsHeightAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Border Height="120" Background="#ffffff">
                <Border a:Web.Id="inner" Background="#eeeeee">
                  <ScrollViewer><StackPanel a:Web.Id="column"><Border Height="400" Background="#cccccc" /></StackPanel></ScrollViewer>
                </Border>
              </Border>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rule(string id) => Regex.Match(page.Css, $@"\.{Regex.Match(html, $"id=\"{id}\"[^>]* class=\"(?:c )?(?<class>k[0-9a-f]+)").Groups["class"].Value}\{{(?<rule>[^}}]*)\}}").Groups["rule"].Value;

        Assert.Contains("flex:1 1 0%;min-height:0;", Rule("inner"), StringComparison.Ordinal);
        Assert.Contains("flex:1 0 auto;", Rule("column"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-68")]
    public async Task ViewersFollowTheirEndAndButtonsScrollThereAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <ScrollViewer Name="Log" a:Web.FollowsEnd="True" Height="100"><StackPanel><Border Height="400" Background="#cccccc" /></StackPanel></ScrollViewer>
              <ScrollViewer Name="Other" Height="100"><Border Height="400" /></ScrollViewer>
              <Button Content="End" a:Web.ScrollsToEnd="Log" />
            </StackPanel>
            """, [null!]);
        var html = EngineFixture.Body(page);
        var viewer = Regex.Match(html, "<div data-aioros-follow data-aioros-scroll-name=\"Log\" class=\"(?<classes>[^\"]+)\"").Groups["classes"].Value;

        Assert.NotEmpty(viewer);
        Assert.Contains("ff", viewer.Split(' '));
        Assert.Contains(".ff{flex-direction:column-reverse}.ff::before{content:\"\";flex:1 0 0}.ff.ff>*{flex-grow:0}", page.Css, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "data-aioros-scroll-name"));
        Assert.Matches("<button [^>]*data-aioros-scrolls=\"Log\"[^>]*>End</button>", html);
        Assert.Contains("[data-aioros-scrolls][data-aioros-at-end]{display:none!important}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-69")]
    public async Task AScrollViewersOwnTemplateKeepsItsContentsHeightAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ScrollViewer a:Web.Id="viewer" Width="200" Height="200" HorizontalScrollBarVisibility="Auto">
                <ScrollViewer.Template>
                  <ControlTemplate>
                    <Grid ColumnDefinitions="*,Auto" RowDefinitions="*,Auto">
                      <ScrollContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                      <ScrollBar Name="PART_VerticalScrollBar" Grid.Column="1" Orientation="Vertical" />
                      <ScrollBar Name="PART_HorizontalScrollBar" Grid.Row="1" Orientation="Horizontal" />
                    </Grid>
                  </ControlTemplate>
                </ScrollViewer.Template>
                <UniformGrid Rows="2" Columns="2" Width="300" Height="300">
                  <Rectangle Fill="#ff0000" /><Rectangle Fill="#00ff00" /><Rectangle Fill="#0000ff" /><Rectangle Fill="#000000" />
                </UniformGrid>
              </ScrollViewer>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        var inside = html[html.IndexOf("id=\"viewer\"", StringComparison.Ordinal)..];
        var classes = Regex.Matches(inside, "class=\"(?<classes>[^\"]+)\"").SelectMany(match => match.Groups["classes"].Value.Split(' ')).Distinct().ToList();

        Assert.DoesNotContain(classes, name => Regex.IsMatch(page.Css, $@"\.{Regex.Escape(name)}\{{[^}}]*flex:1 1 0%"));
        Assert.Contains(classes, name => Regex.IsMatch(page.Css, $@"\.{Regex.Escape(name)}\{{[^}}]*flex:1 0 auto"));
    }
    [Fact]
    [Trait("AC", "LAYOUT-89")]
    public async Task AFlowDirectionIsWrittenWhereItChangesAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel a:Web.Id="rtl" FlowDirection="RightToLeft">
                <TextBlock a:Web.Id="inherits" Text="مرحبا" />
                <StackPanel a:Web.Id="island" FlowDirection="LeftToRight" Orientation="Horizontal"><TextBlock Text="pnpm dev" /></StackPanel>
              </StackPanel>
              <TextBlock a:Web.Id="plain" Text="Hello" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Element(string id) => Regex.Match(html, $"<[a-z]+[^>]*id=\"{id}\"[^>]*>").Value;
        Assert.Contains(" dir=\"rtl\"", Element("rtl"), StringComparison.Ordinal);
        Assert.DoesNotContain(" dir=", Element("inherits"), StringComparison.Ordinal);
        Assert.Contains(" dir=\"ltr\"", Element("island"), StringComparison.Ordinal);
        Assert.DoesNotContain(" dir=", Element("plain"), StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, " dir="));
    }
    [Fact]
    [Trait("AC", "LAYOUT-90")]
    public async Task AScrollFadeIsWrittenAsShadcnsUtilityAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ScrollViewer a:Web.Id="y" a:Web.ScrollFade="y" Height="40"><TextBlock Text="tall" Height="200" /></ScrollViewer>
              <ScrollViewer a:Web.Id="x" a:Web.ScrollFade="x" HorizontalScrollBarVisibility="Auto"><TextBlock Text="wide" Width="900" /></ScrollViewer>
              <ScrollViewer a:Web.Id="b" a:Web.ScrollFade="b" Height="40"><TextBlock Text="end" Height="200" /></ScrollViewer>
              <ScrollViewer a:Web.Id="none" a:Web.ScrollFade="diagonal" Height="40"><TextBlock Text="plain" Height="200" /></ScrollViewer>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string[] Classes(string id) => Regex.Match(html, $"<[a-z]+[^>]*id=\"{id}\"[^>]*class=\"([^\"]*)\"|<[a-z]+[^>]*class=\"([^\"]*)\"[^>]*id=\"{id}\"") is var match && match.Groups[1].Success ? match.Groups[1].Value.Split(' ') : match.Groups[2].Value.Split(' ');
        Assert.Contains("sfy", Classes("y"));
        Assert.Contains("sfx", Classes("x"));
        Assert.Contains("sfb", Classes("b"));
        Assert.DoesNotContain(Classes("none"), name => name.StartsWith("sf", StringComparison.Ordinal));
        Assert.Contains(".sfy{mask-image:linear-gradient(to bottom,transparent 0,#000 var(--sf-t,0px),#000 calc(100% - var(--sf-b,0px)),transparent 100%);mask-repeat:no-repeat;mask-composite:intersect}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@supports (animation-timeline:scroll()){.sfy{animation:asft 1ms ease-in-out,asfb 1ms ease-in-out;animation-timeline:scroll(self y),scroll(self y);animation-range:0 96px,calc(100% - 96px) 100%;animation-fill-mode:both}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@supports not (animation-timeline:scroll()){.sfy{--sf-t:min(12%,40px);--sf-b:min(12%,40px);}}", page.Css, StringComparison.Ordinal);
        Assert.Contains(".sfx:where([dir=rtl],[dir=rtl] *){mask-image:linear-gradient(to left,", page.Css, StringComparison.Ordinal);
        Assert.Contains("animation-timeline:scroll(self inline),scroll(self inline)", page.Css, StringComparison.Ordinal);
        Assert.Contains(".sfb{mask-image:linear-gradient(to bottom,#000 0,#000 calc(100% - var(--sf-b,0px)),transparent 100%)", page.Css, StringComparison.Ordinal);
        Assert.Contains("@keyframes asfb{from{--sf-b:min(12%,40px)}to{--sf-b:0px}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@property --sf-t{syntax:\"<length-percentage>\";inherits:false;initial-value:0px}", page.Css, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(page.Css, @"@keyframes asft\{"));
    }
    [Fact]
    [Trait("AC", "LAYOUT-93")]
    public async Task AScrollFadeOnAViewerFollowingItsEndIsReversedAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ScrollViewer a:Web.Id="end" a:Web.ScrollFade="y" a:Web.FollowsEnd="True" Height="40"><TextBlock Text="tall" Height="200" /></ScrollViewer>
              <ScrollViewer a:Web.Id="start" a:Web.ScrollFade="y" Height="40"><TextBlock Text="tall" Height="200" /></ScrollViewer>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string[] Classes(string id) => Regex.Match(html, $"<[a-z]+[^>]*id=\"{id}\"[^>]*class=\"([^\"]*)\"|<[a-z]+[^>]*class=\"([^\"]*)\"[^>]*id=\"{id}\"") is var match && match.Groups[1].Success ? match.Groups[1].Value.Split(' ') : match.Groups[2].Value.Split(' ');
        Assert.Contains("sfyr", Classes("end"));
        Assert.DoesNotContain("sfy", Classes("end"));
        Assert.Contains("sfy", Classes("start"));
        Assert.Contains("@supports (animation-timeline:scroll()){.sfyr{animation:asftr 1ms ease-in-out,asfbr 1ms ease-in-out;animation-timeline:scroll(self y),scroll(self y);animation-range:calc(100% - 96px) 100%,0 96px;animation-fill-mode:both}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@keyframes asftr{from{--sf-t:min(12%,40px)}to{--sf-t:0px}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@keyframes asfbr{from{--sf-b:0px}to{--sf-b:min(12%,40px)}}", page.Css, StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "LAYOUT-91")]
    public async Task HeightLimitsAreWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ContentControl a:Web.Id="limited" MaxHeight="192"><ScrollViewer><TextBlock Text="tall" Height="400" /></ScrollViewer></ContentControl>
              <Border a:Web.Id="free" Background="#eeeeee"><TextBlock Text="free" /></Border>
              <Grid a:Web.Id="floor" MinHeight="240"><Button Content="Send" VerticalAlignment="Bottom" /></Grid>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rules(string id)
        {
            var classes = Regex.Match(html, $"<[a-z]+[^>]*id=\"{id}\"[^>]*class=\"([^\"]*)\"").Groups[1].Value.Split(' ');
            return string.Concat(classes.Select(name => Regex.Match(page.Css, $@"\.{name}\{{([^}}]*)\}}").Groups[1].Value));
        }

        Assert.Contains("max-height:192px;", Rules("limited"), StringComparison.Ordinal);
        Assert.DoesNotContain("max-height", Rules("free"), StringComparison.Ordinal);
        Assert.Contains("min-height:240px;", Rules("floor"), StringComparison.Ordinal);
        Assert.DoesNotContain("min-height", Rules("free"), StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "LAYOUT-92")]
    public async Task ATemplatesPartsTakeTheirStateLooksAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Card" TargetType="ContentControl">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Panel>
                        <Border Name="PART_Box" Background="#f5f5f5" Padding="16"><ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" /></Border>
                        <Border Name="PART_Float" Width="24" Height="24" Background="#111111" Opacity="0" HorizontalAlignment="Right" VerticalAlignment="Top" />
                      </Panel>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:pointerover /template/ Border#PART_Float"><Setter Property="Opacity" Value="1" /></Style>
                  <Style Selector="^:focus-within /template/ Border#PART_Box"><Setter Property="Background" Value="#e5e5e5" /></Style>
                </ControlTheme>
              </StackPanel.Resources>
              <ContentControl a:Web.Id="card" Theme="{StaticResource Card}"><TextBox Text="code" /></ContentControl>
            </StackPanel>
            """);

        Assert.Matches(@"\.h[0-9a-f]+:hover \.d[0-9a-f]+\{opacity:1!important;\}", page.Css);
        Assert.Matches(@"\.h[0-9a-f]+:focus-within \.d[0-9a-f]+\{background:#e5e5e5!important;\}", page.Css);
        Assert.Matches("id=\"card\"[^>]*class=\"[^\"]*h[0-9a-f]+", EngineFixture.Body(page));
    }

    [Fact]
    [Trait("AC", "LAYOUT-98")]
    public async Task StackedGridsKeepAGapBetweenTheirCellsAsync()
    {
        async Task<double> GapAsync(string spacing)
        {
            var page = await fixture.CompileAsync($"""
                <StackPanel>
                  <Grid ColumnDefinitions="*,*" {spacing} a:Web.StackBelow="640">
                    <TextBlock Text="a" />
                    <TextBlock Grid.Column="1" Text="b" />
                  </Grid>
                </StackPanel>
                """);
            var texts = await BrowserLayout.TextsAsync(page, 400, 400);
            var (first, second) = (texts.Single(static text => text.Text == "a"), texts.Single(static text => text.Text == "b"));
            return second.Y - first.Y;
        }

        var none = await GapAsync(string.Empty);

        Assert.Equal(16, Math.Round(await GapAsync("ColumnSpacing=\"16\"") - none));
        Assert.Equal(8, Math.Round(await GapAsync("ColumnSpacing=\"16\" RowSpacing=\"8\"") - none));
    }
}
