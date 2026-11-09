using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Markup;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class MediaControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "MEDIA-01")]
    public async Task CanvasesAreSvgAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Canvas Width="100" Height="50">
              <Polyline Points="0,0 50,25 100,0" Stroke="Red" StrokeThickness="2" />
              <Rectangle Canvas.Left="10" Canvas.Top="5" Width="20" Height="10" Fill="#00ff00" />
              <Ellipse Canvas.Left="60" Canvas.Top="10" Width="20" Height="20" Fill="Blue" IsVisible="False" />
            </Canvas>
            """);

        Assert.Matches("<svg width=\"100\" height=\"50\" class=\"[^\"]+\"><polyline points=\"0,0 50,25 100,0\" fill=\"none\" stroke=\"#ff0000\" stroke-width=\"2\" stroke-miterlimit=\"10\"/>", html);
        Assert.Contains("""<rect transform="translate(10 5)" width="20" height="10" fill="#00ff00"/></svg>""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("viewBox", html, StringComparison.Ordinal);
        Assert.Contains("pointer-events:none", (await fixture.CompileAsync("<Canvas Width=\"100\" Height=\"50\" />")).Css, StringComparison.Ordinal);
        Assert.DoesNotContain("pointer-events:none", (await fixture.CompileAsync("<Canvas Width=\"100\" Height=\"50\" Background=\"White\" />")).Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-02")]
    public async Task PathsAreSvgPathsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Path Data="M0,0 L20,0 L20,10 Z" Fill="Red" Stroke="Blue" StrokeThickness="2" StrokeDashArray="2,1" StrokeLineCap="Round" StrokeJoin="Bevel" HorizontalAlignment="Left" />
              <Path Data="M0,0 L20,0 L20,10 Z" Fill="Red" Stretch="Uniform" Width="40" Height="40" />
            </StackPanel>
            """);

        Assert.Matches("<svg width=\"20\" height=\"10\" viewBox=\"0 0 20 10\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M0 0L20 0L20 10L0 0Z\" fill=\"#ff0000\" stroke=\"#0000ff\" stroke-width=\"2\" stroke-dasharray=\"4 2\" stroke-linecap=\"round\" stroke-linejoin=\"bevel\" fill-rule=\"evenodd\"/></svg>", html);
        Assert.Matches("<svg width=\"40\" height=\"40\" viewBox=\"0 0 20 10\" preserveAspectRatio=\"xMinYMin meet\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M0 0L20 0L20 10L0 0Z\" fill=\"#ff0000\" fill-rule=\"evenodd\"/></svg>", html);
    }

    [Fact]
    [Trait("AC", "MEDIA-03")]
    public async Task PathIconsAreInlineSvgAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <PathIcon Data="M0,0 L10,0 L10,10 Z" />
              <PathIcon Data="M0,0 L10,0 L10,5 Z" Foreground="Red" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("<svg width=\"20\" height=\"20\" viewBox=\"0 0 10 10\" fill=\"currentColor\" class=\"k[0-9a-f]+\"><path d=\"M0 0L10 0L10 10L0 0Z\" fill-rule=\"evenodd\"/></svg>", html);
        var colored = Assert.Single(Regex.Matches(html, "<svg width=\"20\" height=\"20\" viewBox=\"0 0 10 5\" fill=\"currentColor\" class=\"(k[0-9a-f]+)\">"));
        Assert.Contains($".{colored.Groups[1].Value}{{align-self:center;flex-shrink:0;color:#ff0000;}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-04")]
    public async Task RectanglesAndEllipsesAreBoxesAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Rectangle Width="40" Height="20" Fill="Red" Stroke="Blue" StrokeThickness="2" RadiusX="4" RadiusY="4" HorizontalAlignment="Left" />
              <Ellipse Width="30" Height="30" Fill="Green" HorizontalAlignment="Left" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("^<div class=\"c k[0-9a-f]+\"><div class=\"k[0-9a-f]+\"></div><div class=\"k[0-9a-f]+\"></div></div>$", html);
        Assert.Contains("width:40px;height:20px;align-self:flex-start;flex-shrink:0;background:#ff0000;border:2px solid #0000ff;border-radius:5px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("width:30px;height:30px;align-self:flex-start;flex-shrink:0;background:#008000;border-radius:50%;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-05")]
    public async Task ShapesOutsideCanvasesAreSvgAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Line StartPoint="0,0" EndPoint="30,10" Stroke="Red" StrokeThickness="2" />
              <Polyline Points="0,0 10,5 20,0" Stroke="Green" StrokeThickness="1" />
              <Polygon Points="0,0 10,0 5,8" Fill="Blue" />
              <Sector Width="30" Height="30" StartAngle="0" SweepAngle="90" Fill="Orange" />
              <Arc Width="20" Height="20" StartAngle="0" SweepAngle="90" Stroke="Red" StrokeThickness="2" />
            </StackPanel>
            """);

        Assert.Matches("<svg width=\"30\" height=\"10\" viewBox=\"0 0 30 10\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M0 0L30 10\" fill=\"none\" stroke=\"#ff0000\" stroke-width=\"2\" stroke-miterlimit=\"10\"/></svg>", html);
        Assert.Matches("<svg width=\"20\" height=\"5\" viewBox=\"0 0 20 5\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M0 0L10 5L20 0\" fill=\"none\" stroke=\"#008000\" stroke-width=\"1\" stroke-miterlimit=\"10\"/></svg>", html);
        Assert.Matches("<svg width=\"30\" height=\"30\" viewBox=\"0 0 30 30\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M[^\"]+Z\" fill=\"#ffa500\"", html);
        Assert.Matches("<svg width=\"10\" height=\"8\" viewBox=\"0 0 10 8\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M0 0L10 0L5 8L0 0Z\" fill=\"#0000ff\"", html);
        Assert.Matches("<svg width=\"20\" height=\"20\" viewBox=\"0 0 20 20\" overflow=\"visible\" class=\"k[0-9a-f]+\"><path d=\"M[^\"]+\" fill=\"none\" stroke=\"#ff0000\" stroke-width=\"2\" stroke-miterlimit=\"10\"/></svg>", html);
    }

    [Fact]
    [Trait("AC", "MEDIA-06")]
    public async Task ImagesAreDataUriImgsAsync()
    {
        var page = await fixture.CompileAsync("""
            <Image Source="avares://Aioros.Avalonia.Tests/Assets/dot.png" Width="40" Stretch="UniformToFill" AutomationProperties.Name="A red dot" HorizontalAlignment="Left" />
            """);
        var html = EngineFixture.Body(page, [null]);

        var image = Assert.Single(Regex.Matches(html, "^<img src=\"data:image/png;base64,([A-Za-z0-9+/=]+)\" alt=\"A red dot\" width=\"2\" height=\"2\" class=\"(k[0-9a-f]+)\">$"));
        var png = Convert.FromBase64String(image.Groups[1].Value);
        Assert.Equal((2, 2), (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20))));
        Assert.Contains($".{image.Groups[2].Value}{{width:40px;align-self:flex-start;flex:1 1 0%;min-height:0;height:auto;object-fit:cover;}}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-07")]
    public async Task ViewboxesScaleTheirChildAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Viewbox Width="100" Height="50"><Border Width="20" Height="20" Background="Red" /></Viewbox>
              <Viewbox Width="100" Height="50" Stretch="Fill"><Border Width="20" Height="10" Background="Blue" /></Viewbox>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("<svg viewBox=\"0 0 20 20\" preserveAspectRatio=\"xMidYMid meet\" class=\"k[0-9a-f]+\"><foreignObject width=\"20\" height=\"20\"><div class=\"c k[0-9a-f]+\"></div></foreignObject></svg>", html);
        Assert.Matches("<svg viewBox=\"0 0 20 10\" preserveAspectRatio=\"none\" class=\"k[0-9a-f]+\"><foreignObject width=\"20\" height=\"10\">", html);
        Assert.Contains("width:100px;height:50px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-08")]
    public async Task IconsDrawNativelyAsync()
    {
        var drawn = await fixture.CaptureAsync("""<a:Icon Kind="check" Size="48" StrokeWidth="3" Foreground="#ff0000" HorizontalAlignment="Left" VerticalAlignment="Top" />""", width: 64, height: 64);
        var empty = await fixture.CaptureAsync("""<a:Icon Kind="no-such-icon" Size="48" Foreground="#ff0000" HorizontalAlignment="Left" VerticalAlignment="Top" />""", width: 64, height: 64);

        Assert.True(Pixels.Red(drawn) > 40);
        Assert.Equal(0, Pixels.Red(empty));
    }

    [Fact]
    [Trait("AC", "MEDIA-24")]
    public async Task AnIconTakesTheSizeItsParentGivesIconsAsync()
    {
        const string Body = """
            <StackPanel HorizontalAlignment="Left">
              <StackPanel.Styles>
                <Style Selector="Button.xs"><Setter Property="a:IconStyle.Size" Value="12" /></Style>
              </StackPanel.Styles>
              <Button Classes="xs"><StackPanel Orientation="Horizontal"><a:Icon Kind="check" /><a:Icon Kind="check" Size="20" /></StackPanel></Button>
              <a:Icon Kind="check" HorizontalAlignment="Left" />
            </StackPanel>
            """;
        var html = await fixture.HtmlAsync(Body);
        var widths = Regex.Matches(html, "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"([0-9]+)\"").Select(static match => match.Groups[1].Value).ToList();
        var desktop = await fixture.Host.InvokeAsync(() =>
        {
            var inner = new Aioros.Avalonia.Controls.Icon { Kind = "check" };
            var sized = new Aioros.Avalonia.Controls.Icon { Kind = "check", Size = 20 };
            var outside = new Aioros.Avalonia.Controls.Icon { Kind = "check", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
            var button = new Button { Classes = { "xs" }, Content = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Children = { inner, sized } } };
            var root = new StackPanel { HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left, Children = { button, outside } };
            root.Styles.Add(new global::Avalonia.Styling.Style(selector => selector.OfType<Button>().Class("xs")) { Setters = { new global::Avalonia.Styling.Setter(IconStyle.SizeProperty, 12.0) } });
            var window = new Window { Width = 200, Height = 120, Content = root };
            window.Show();
            window.UpdateLayout();
            var bounds = new[] { inner.Bounds.Width, sized.Bounds.Width, outside.Bounds.Width };
            window.Close();
            return bounds;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(["12", "20", "16"], widths);
        Assert.Equal([12.0, 20.0, 16.0], desktop);
    }

    [Fact]
    [Trait("AC", "MEDIA-10")]
    public async Task IconsTakeTheirStrokeLookFromTheirClassAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <a:Icon Kind="check" Size="20" StrokeWidth="3" Foreground="#ff0000" />
              <a:Icon Kind="check" Size="20" StrokeWidth="3" Foreground="#ff0000" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        var icon = Regex.Match(html, "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"20\" height=\"20\" class=\"(k[0-9a-f]+)\"><path d=\"M20 6 9 17l-5-5\"/></svg>");
        Assert.True(icon.Success, html);
        Assert.Equal(2, Regex.Count(html, Regex.Escape(icon.Value)));
        Assert.DoesNotContain("stroke=", html, StringComparison.Ordinal);
        Assert.Contains($".{icon.Groups[1].Value}{{flex-shrink:0;fill:none;stroke:currentColor;stroke-width:3;stroke-linecap:round;stroke-linejoin:round;color:#ff0000;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-09")]
    public async Task ImagesFromUrlsAreLazyImgsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="m:Poster">
                    <Image loader:ImageLoader.Source="{Binding Url}" AutomationProperties.Name="{Binding Title}" Height="100" Stretch="UniformToFill" />
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <Image loader:ImageLoader.Source="/art/fixed.png" Width="40" Height="40" />
              <Image loader:ImageLoader.Source="/art/fill.png" Width="40" Height="40" Stretch="Fill" />
            </StackPanel>
            """, new Posters(new Poster("/art/1.png", "One")));
        var html = EngineFixture.Body(page, [new Posters(new Poster("/art/2.png", "Two & three"), new Poster("/art/3.png", "Four"))]);

        Assert.True(page.Reusable, page.Failure);
        var bound = Regex.Match(html, "<img src=\"/art/2.png\" alt=\"Two &amp; three\" loading=\"lazy\" decoding=\"async\" class=\"(k[0-9a-f]+)\">");
        Assert.True(bound.Success, html);
        Assert.Contains("<img src=\"/art/3.png\" alt=\"Four\" loading=\"lazy\" decoding=\"async\"", html, StringComparison.Ordinal);
        Assert.Matches($"\\.{bound.Groups[1].Value}\\{{[^}}]*width:100%;[^}}]*object-fit:cover;", page.Css);
        var contain = Regex.Match(html, "<img src=\"/art/fixed.png\" alt=\"\" loading=\"lazy\" decoding=\"async\" class=\"(k[0-9a-f]+)\">");
        var fill = Regex.Match(html, "<img src=\"/art/fill.png\"[^>]* class=\"(k[0-9a-f]+)\">");
        Assert.Matches($"\\.{contain.Groups[1].Value}\\{{[^}}]*object-fit:contain;", page.Css);
        Assert.Matches($"\\.{fill.Groups[1].Value}\\{{[^}}]*object-fit:fill;", page.Css);
    }

    [Fact]
    [Trait("AC", "MEDIA-11")]
    public async Task CanvasChildrenArePlacedAsTheCanvasArrangesThemAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Canvas Width="100" Height="50" HorizontalAlignment="Left">
              <Rectangle Width="20" Height="10" Fill="Red" />
              <Rectangle Canvas.Left="30" Canvas.Top="5" Width="20" Height="10" Fill="Lime" />
              <Rectangle Canvas.Right="10" Canvas.Bottom="5" Width="20" Height="10" Fill="Blue" />
              <Rectangle Canvas.Left="4" Canvas.Bottom="2" Width="6" Height="6" Fill="Black" />
            </Canvas>
            """);

        Assert.DoesNotContain("NaN", html, StringComparison.Ordinal);
        Assert.Contains("""<rect width="20" height="10" fill="#ff0000"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<rect transform="translate(30 5)" width="20" height="10" fill="#00ff00"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<rect transform="translate(70 35)" width="20" height="10" fill="#0000ff"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<rect transform="translate(4 42)" width="6" height="6" fill="#000000"/>""", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-12")]
    public async Task CanvasShapesAreTheirRenderedGeometryAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Canvas Width="100" Height="100" HorizontalAlignment="Left">
              <Path Data="M10,10 L40,10 L40,30" Stroke="Red" StrokeThickness="2" />
              <Path Canvas.Left="60" Canvas.Top="60" Data="M0,0 L10,0 L10,5 Z" Width="40" Height="40" Stretch="Uniform" Fill="Lime" />
              <Path Fill="Blue">
                <Path.Data>
                  <GeometryGroup FillRule="NonZero">
                    <RectangleGeometry Rect="0,40,10,10" />
                    <EllipseGeometry Center="30,45" RadiusX="5" RadiusY="5" />
                  </GeometryGroup>
                </Path.Data>
              </Path>
              <Rectangle Canvas.Left="0" Canvas.Top="60" Width="20" Height="10" Stroke="Red" StrokeThickness="2" />
              <Ellipse Canvas.Left="30" Canvas.Top="60" Width="20" Height="10" Fill="Red" />
              <Line StartPoint="0,90" EndPoint="50,90" Stroke="Red" StrokeThickness="1" />
              <Polygon Points="70,0 90,0 80,20" Fill="Blue" />
            </Canvas>
            """);

        Assert.Contains("""<path d="M10 10L40 10L40 30" fill="none" stroke="#ff0000" stroke-width="2" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<path transform="translate(60 60)" d="M0 0L40 0L40 20L0 0Z" fill="#00ff00" fill-rule="evenodd"/>""", html, StringComparison.Ordinal);
        Assert.Matches("""<path d="M0 40L10 40L10 50L0 50[^"]*M35 45[^"]*" fill="#0000ff"/>""", html);
        Assert.Contains("""<rect transform="translate(0 60)" x="1" y="1" width="18" height="8" fill="none" stroke="#ff0000" stroke-width="2" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<ellipse transform="translate(30 60)" cx="10" cy="5" rx="10" ry="5" fill="#ff0000"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<path d="M0 90L50 90" fill="none" stroke="#ff0000" stroke-width="1" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<polygon points="70,0 90,0 80,20" fill="#0000ff" fill-rule="evenodd"/>""", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-13")]
    public async Task StrokesKeepAvaloniasPenAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Canvas Width="100" Height="40" HorizontalAlignment="Left">
                <Polyline Points="0,0 10,10 20,0" Stroke="Red" StrokeThickness="2" />
                <Line StartPoint="0,30" EndPoint="60,30" Stroke="Blue" StrokeThickness="2" StrokeLineCap="Square" StrokeDashArray="2,1" StrokeDashOffset="1" />
                <Polyline Points="30,0 40,10 50,0" Stroke="Green" StrokeThickness="2" StrokeJoin="Round" StrokeLineCap="Round" />
              </Canvas>
              <Polyline Points="0,0 10,10 20,0" Stroke="Red" StrokeThickness="2" StrokeMiterLimit="4" HorizontalAlignment="Left" />
              <Polyline Points="0,0 10,10 20,0" Stroke="Red" StrokeThickness="2" StrokeJoin="Bevel" HorizontalAlignment="Left" />
              <Polyline Points="0,0 10,10 20,0" Stroke="Red" StrokeThickness="2" HorizontalAlignment="Left" />
            </StackPanel>
            """);

        Assert.Contains("""<polyline points="0,0 10,10 20,0" fill="none" stroke="#ff0000" stroke-width="2" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<path d="M0 30L60 30" fill="none" stroke="#0000ff" stroke-width="2" stroke-dasharray="4 2" stroke-dashoffset="2" stroke-linecap="square" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<polyline points="30,0 40,10 50,0" fill="none" stroke="#008000" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("vector-effect", html, StringComparison.Ordinal);
        Assert.Contains("""<path d="M0 0L10 10L20 0" fill="none" stroke="#ff0000" stroke-width="2"/></svg>""", html, StringComparison.Ordinal);
        Assert.Contains("""<path d="M0 0L10 10L20 0" fill="none" stroke="#ff0000" stroke-width="2" stroke-linejoin="bevel"/></svg>""", html, StringComparison.Ordinal);
        Assert.Contains("""<path d="M0 0L10 10L20 0" fill="none" stroke="#ff0000" stroke-width="2" stroke-miterlimit="10"/></svg>""", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-14")]
    public async Task LinearGradientBrushesOnShapesAreSvgGradientsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Canvas Width="100" Height="50" HorizontalAlignment="Left">
                <Rectangle Width="100" Height="50" RadiusX="10" RadiusY="10">
                  <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%" Opacity="0.5"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="#800000FF" /></LinearGradientBrush>
                  </Rectangle.Fill>
                </Rectangle>
                <Path Canvas.Top="20" Data="M0,0 L40,0" StrokeThickness="4">
                  <Path.Stroke>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="20,0" SpreadMethod="Reflect"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                  </Path.Stroke>
                </Path>
                <Path Canvas.Top="30" Data="M0,0 L40,0" StrokeThickness="4">
                  <Path.Stroke>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%" SpreadMethod="Repeat"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                  </Path.Stroke>
                </Path>
              </Canvas>
              <Path Data="M0,0 L20,0 L20,10 Z" Width="40" Height="40" Stretch="Fill" HorizontalAlignment="Left">
                <Path.Fill>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Path.Fill>
              </Path>
              <Path Data="M0,0 L20,0 L20,10 Z" HorizontalAlignment="Left">
                <Path.Fill>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Lime" /></LinearGradientBrush>
                </Path.Fill>
              </Path>
            </StackPanel>
            """);

        var fill = Regex.Match(html, """<linearGradient id="(vg[0-9a-f]{8})" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="100" y2="50"><stop offset="0" stop-color="#ff0000" stop-opacity="0.5"/><stop offset="1" stop-color="#0000ff" stop-opacity="0.251"/></linearGradient>""");
        Assert.True(fill.Success, html);
        Assert.Contains($"""<rect width="100" height="50" rx="10" ry="10" fill="url(#{fill.Groups[1].Value})"/>""", html, StringComparison.Ordinal);
        var reflected = Regex.Match(html, """<linearGradient id="(vg[0-9a-f]{8})" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="20" y2="0" spreadMethod="reflect"><stop offset="0" stop-color="#ff0000"/><stop offset="1" stop-color="#0000ff"/></linearGradient>""");
        Assert.True(reflected.Success, html);
        Assert.Contains($"""<path transform="translate(0 20)" d="M0 0L40 0" fill="none" stroke="url(#{reflected.Groups[1].Value})" stroke-width="4" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Matches("""<linearGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" x1="-2" y1="-2" x2="42" y2="-2" spreadMethod="repeat">""", html);
        Assert.Matches("""<svg width="40" height="40" viewBox="0 0 20 10" preserveAspectRatio="none" overflow="visible" class="k[0-9a-f]+"><path d="M0 0L20 0L20 10L0 0Z" fill="url\(#vg[0-9a-f]{8}\)" fill-rule="evenodd"/><defs><linearGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="40" y2="40" gradientTransform="matrix\(0.5 0 0 0.25 0 0\)">""", html);
        Assert.Matches("""<svg width="20" height="10" viewBox="0 0 20 10" overflow="visible" class="k[0-9a-f]+"><path d="M0 0L20 0L20 10L0 0Z" fill="url\(#vg[0-9a-f]{8}\)" fill-rule="evenodd"/><defs><linearGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="20" y2="10"><stop offset="0" stop-color="#ff0000"/><stop offset="1" stop-color="#00ff00"/>""", html);
    }

    [Fact]
    [Trait("AC", "MEDIA-15")]
    public async Task RadialGradientBrushesOnShapesAreSvgGradientsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Canvas Width="100" Height="100" HorizontalAlignment="Left">
              <Rectangle Width="100" Height="50">
                <Rectangle.Fill>
                  <RadialGradientBrush GradientOrigin="25%,50%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></RadialGradientBrush>
                </Rectangle.Fill>
              </Rectangle>
              <Ellipse Canvas.Top="50" Width="40" Height="40">
                <Ellipse.Fill>
                  <RadialGradientBrush SpreadMethod="Reflect" RadiusX="25%" RadiusY="25%"><GradientStop Offset="0" Color="Lime" /><GradientStop Offset="1" Color="Blue" /></RadialGradientBrush>
                </Ellipse.Fill>
              </Ellipse>
              <Ellipse Canvas.Left="50" Canvas.Top="50" Width="40" Height="40">
                <Ellipse.Fill>
                  <RadialGradientBrush TransformOrigin="50%,50%">
                    <RadialGradientBrush.Transform><ScaleTransform ScaleX="2" /></RadialGradientBrush.Transform>
                    <GradientStop Offset="0" Color="Yellow" /><GradientStop Offset="1" Color="Blue" />
                  </RadialGradientBrush>
                </Ellipse.Fill>
              </Ellipse>
            </Canvas>
            """);

        Assert.Matches("""<radialGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" cx="50" cy="25" r="50" fx="25" fy="25" gradientTransform="matrix\(1 0 0 0.5 0 12.5\)"><stop offset="0" stop-color="#ff0000"/><stop offset="1" stop-color="#0000ff"/></radialGradient>""", html);
        Assert.Matches("""<radialGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" cx="20" cy="20" r="10" spreadMethod="reflect"><stop offset="0" stop-color="#00ff00"/>""", html);
        Assert.Matches("""<radialGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" cx="20" cy="20" r="20" gradientTransform="matrix\(2 0 0 1 -20 0\)"><stop offset="0" stop-color="#ffff00"/>""", html);
        Assert.Equal(3, Regex.Count(html, """fill="url\(#vg[0-9a-f]{8}\)"""));
    }

    [Fact]
    [Trait("AC", "MEDIA-16")]
    public async Task CanvasesPlaceAnyChildInTheirOrderWithTheirLookAsync()
    {
        var page = await fixture.CompileAsync("""
            <Canvas Width="100" Height="50" HorizontalAlignment="Left">
              <Canvas.Background>
                <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
              </Canvas.Background>
              <Rectangle Width="10" Height="10" Fill="Red" ZIndex="2" />
              <Rectangle Width="10" Height="10" Fill="Lime" Opacity="0.5" />
              <Rectangle Canvas.Left="20" Width="10" Height="20" Fill="Blue" RenderTransform="rotate(90deg)" />
              <TextBlock Canvas.Left="40" Canvas.Top="8" Text="Label" />
            </Canvas>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.True(html.IndexOf("#00ff00", StringComparison.Ordinal) < html.IndexOf("""fill="#ff0000""", StringComparison.Ordinal), html);
        Assert.Contains("""<rect opacity="0.5" width="10" height="10" fill="#00ff00"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<rect transform="matrix(0 1 -1 0 35 5)" width="10" height="20" fill="#0000ff"/>""", html, StringComparison.Ordinal);
        Assert.Matches("""<foreignObject x="40" y="8" width="[0-9.]+" height="[0-9.]+" overflow="visible"><[^>]+>Label<""", html);
        Assert.Contains("background:linear-gradient(90deg,#ff0000 0%,#0000ff 100%);", page.Css, StringComparison.Ordinal);
        Assert.Contains("overflow:visible;", page.Css, StringComparison.Ordinal);
        var clipped = await fixture.CompileAsync("""<Canvas Width="10" Height="10" ClipToBounds="True"><Rectangle Width="20" Height="20" Fill="Red" /></Canvas>""");
        Assert.Contains("overflow:hidden;", clipped.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow:visible;", clipped.Css, StringComparison.Ordinal);
        var templated = await fixture.HtmlAsync("""
            <TemplatedControl HorizontalAlignment="Left">
              <TemplatedControl.Template>
                <ControlTemplate>
                  <Canvas Width="100" Height="20"><Rectangle Width="10" Height="10" Fill="Red" /><TextBlock Canvas.Left="20" Text="Moving" /></Canvas>
                </ControlTemplate>
              </TemplatedControl.Template>
            </TemplatedControl>
            """);
        Assert.Contains("""<rect width="10" height="10" fill="#ff0000"/>""", templated, StringComparison.Ordinal);
        Assert.DoesNotContain("Moving", templated, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-20")]
    public async Task ConicGradientFillsAreClippedCssGradientsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Canvas Width="100" Height="50" HorizontalAlignment="Left">
                <Ellipse Canvas.Left="50" Width="40" Height="40" Stroke="Black" StrokeThickness="2">
                  <Ellipse.Fill>
                    <ConicGradientBrush Angle="90"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></ConicGradientBrush>
                  </Ellipse.Fill>
                </Ellipse>
              </Canvas>
              <Path Data="M0,0 L20,0 L20,10 Z" HorizontalAlignment="Left">
                <Path.Fill>
                  <ConicGradientBrush Center="0%,0%"><GradientStop Offset="0" Color="Lime" /><GradientStop Offset="1" Color="Blue" /></ConicGradientBrush>
                </Path.Fill>
              </Path>
            </StackPanel>
            """);

        var ellipse = Regex.Match(html, """<g transform="translate\(50 0\)" clip-path="url\(#(vc[0-9a-f]{8})\)"><foreignObject x="1" y="1" width="38" height="38"><div style="height:100%;background:conic-gradient\(from 90deg at 19px 19px,#ff0000 0%,#0000ff 100%\)"></div></foreignObject></g><ellipse transform="translate\(50 0\)" cx="20" cy="20" rx="19" ry="19" fill="none" stroke="#000000" stroke-width="2" stroke-miterlimit="10"/>""");
        Assert.True(ellipse.Success, html);
        Assert.Contains($"""<clipPath id="{ellipse.Groups[1].Value}"><ellipse cx="20" cy="20" rx="19" ry="19"/></clipPath>""", html, StringComparison.Ordinal);
        Assert.Matches("""<g clip-path="url\(#vc[0-9a-f]{8}\)"><foreignObject width="20" height="10"><div style="height:100%;background:conic-gradient\(from 0deg at 0px 0px,#00ff00 0%,#0000ff 100%\)"></div></foreignObject></g><defs><clipPath id="vc[0-9a-f]{8}"><path d="M0 0L20 0L20 10L0 0Z" clip-rule="evenodd"/></clipPath></defs></svg>""", html);
    }

    [Fact]
    [Trait("AC", "MEDIA-17")]
    public async Task SizedRectanglesAndEllipsesWithGradientsAreSvgAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Rectangle Width="40" Height="20" RadiusX="4" RadiusY="4" StrokeThickness="2" HorizontalAlignment="Left">
                <Rectangle.Fill>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="Red" /><GradientStop Offset="1" Color="Blue" /></LinearGradientBrush>
                </Rectangle.Fill>
                <Rectangle.Stroke>
                  <LinearGradientBrush StartPoint="0%,0%" EndPoint="0%,100%"><GradientStop Offset="0" Color="Lime" /><GradientStop Offset="1" Color="Black" /></LinearGradientBrush>
                </Rectangle.Stroke>
              </Rectangle>
              <Ellipse Width="30" Height="30" HorizontalAlignment="Left">
                <Ellipse.Fill>
                  <RadialGradientBrush><GradientStop Offset="0" Color="White" /><GradientStop Offset="1" Color="Blue" /></RadialGradientBrush>
                </Ellipse.Fill>
              </Ellipse>
              <Rectangle Width="40" Height="20" Fill="Red" HorizontalAlignment="Left" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("""<svg width="40" height="20" viewBox="0 0 40 20" overflow="visible" class="k[0-9a-f]+"><rect x="1" y="1" width="38" height="18" rx="4" ry="4" fill="url\(#vg[0-9a-f]{8}\)" stroke="url\(#vg[0-9a-f]{8}\)" stroke-width="2" stroke-miterlimit="10"/><defs><linearGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" x1="1" y1="1" x2="39" y2="1">(<stop[^>]+/>){2}</linearGradient><linearGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="0" y2="20">""", html);
        Assert.Matches("""<svg width="30" height="30" viewBox="0 0 30 30" overflow="visible" class="k[0-9a-f]+"><ellipse cx="15" cy="15" rx="15" ry="15" fill="url\(#vg[0-9a-f]{8}\)"/><defs><radialGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" cx="15" cy="15" r="15">""", html);
        Assert.Contains("width:40px;height:20px;align-self:flex-start;flex-shrink:0;background:#ff0000;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MEDIA-18")]
    public async Task ViewboxedCanvasesOfShapesAreDrawnLikeAvaloniaAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Viewbox Width="28" Height="28" HorizontalAlignment="Left">
              <Canvas Width="64" Height="64">
                <Rectangle Width="64" Height="64" RadiusX="14" RadiusY="14">
                  <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%"><GradientStop Offset="0" Color="#2563EB" /><GradientStop Offset="1" Color="#7C3AED" /></LinearGradientBrush>
                  </Rectangle.Fill>
                </Rectangle>
                <Path Width="64" Height="64" Data="M25,39 L19,55 M39,39 L45,55" Stroke="#C7D2FE" StrokeThickness="3.2" StrokeLineCap="Round" />
                <Rectangle Canvas.Left="13" Canvas.Top="9" Width="38" Height="28" RadiusX="4.5" RadiusY="4.5" Fill="#FFFFFF" />
              </Canvas>
            </Viewbox>
            """);

        Assert.Matches("""<svg viewBox="0 0 64 64" preserveAspectRatio="xMidYMid meet" class="k[0-9a-f]+"><foreignObject width="64" height="64"><svg width="64" height="64" class="k[0-9a-f]+"><rect width="64" height="64" rx="14" ry="14" fill="url\(#vg[0-9a-f]{8}\)"/>""", html);
        Assert.Contains("""<path d="M25 39L19 55M39 39L45 55" fill="none" stroke="#c7d2fe" stroke-width="3.2" stroke-linecap="round" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
        Assert.Contains("""<rect transform="translate(13 9)" width="38" height="28" rx="4.5" ry="4.5" fill="#ffffff"/>""", html, StringComparison.Ordinal);
        Assert.Matches("""<linearGradient id="vg[0-9a-f]{8}" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="64" y2="64"><stop offset="0" stop-color="#2563eb"/><stop offset="1" stop-color="#7c3aed"/></linearGradient></defs></svg></foreignObject></svg>""", html);
    }

    [Fact]
    [Trait("AC", "MEDIA-19")]
    public async Task BoundPointsInCanvasesFollowTheModelAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <Canvas Width="100" Height="50" HorizontalAlignment="Left">
              <Polyline Canvas.Left="5" Points="{Binding Line}" Stroke="Red" StrokeThickness="2" />
            </Canvas>
            """, new Traced(1, 2, 3), new Traced(9, 8));
        var html = EngineFixture.Body(page, new Traced(9, 8));

        Assert.Contains("""<polyline transform="translate(5 0)" points="0,9 10,8" fill="none" stroke="#ff0000" stroke-width="2" stroke-miterlimit="10"/>""", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-276")]
    public async Task AnIconDrawsTheMarkupRegisteredLastAsync()
    {
        var found = new List<int>();
        void Strokes() => found.Add(Aioros.Avalonia.Controls.IconGlyphs.Of("engine-276").Length);
        await fixture.Engine.InvokeAsync(Strokes, TestContext.Current.CancellationToken);
        Runtime.Slots.IconSet.Register([new("engine-276", "<path d=\"M4 12h16\"/>")]);
        await fixture.Engine.InvokeAsync(Strokes, TestContext.Current.CancellationToken);
        Runtime.Slots.IconSet.Register([new("engine-276", "<path d=\"M4 12h16\"/><path d=\"M12 4v16\"/>")]);
        await fixture.Engine.InvokeAsync(Strokes, TestContext.Current.CancellationToken);

        Assert.Equal([0, 1, 2], found);
    }

    [Fact]
    [Trait("AC", "MEDIA-21")]
    public async Task IconsALibraryRegistersAreDrawnAsync()
    {
        Runtime.Slots.IconSet.Register([new("test-branch", "<path d=\"M6 3v12\"/><circle cx=\"18\" cy=\"6\" r=\"3\"/>"), new("test-check", "<path d=\"M4 12h16\"/>")]);
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <a:Icon Kind="test-branch" Size="20" />
              <a:Icon Kind="{Binding Name}" Size="20" />
              <a:Icon Kind="check" Size="20" />
            </StackPanel>
            """, new Item("test-check"));
        var drawn = await fixture.CaptureAsync("""<a:Icon Kind="test-check" Size="48" StrokeWidth="4" Foreground="#ff0000" HorizontalAlignment="Left" VerticalAlignment="Top" />""", width: 64, height: 64);

        var html = EngineFixture.Body(page, new Item("test-check"));
        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("<path d=\"M6 3v12\"/><circle cx=\"18\" cy=\"6\" r=\"3\"/></svg>", html, StringComparison.Ordinal);
        Assert.Contains("<path d=\"M4 12h16\"/></svg>", html, StringComparison.Ordinal);
        Assert.Contains("<path d=\"M20 6 9 17l-5-5\"/></svg>", html, StringComparison.Ordinal);
        Assert.True(Pixels.Red(drawn) > 40);
    }

    [Fact]
    [Trait("AC", "MEDIA-22")]
    public async Task DashedRectangleStrokesAreDashedAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Panel Height="40"><Rectangle Stroke="#e5e5e5" StrokeThickness="1" StrokeDashArray="3,3" RadiusX="8" RadiusY="8" /></Panel>
              <Panel Height="40"><Rectangle Stroke="#ff0000" StrokeThickness="2" StrokeDashArray="1,1" /></Panel>
              <Rectangle Width="40" Height="20" Stroke="#0000ff" StrokeThickness="1" StrokeDashArray="3,3" HorizontalAlignment="Left" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("\\{[^}]*border:1px dashed #e5e5e5;[^}]*border-radius:8.5px;", page.Css);
        Assert.Contains("border:2px dotted #ff0000;", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("solid #0000ff", page.Css, StringComparison.Ordinal);
        Assert.Matches("<svg width=\"40\" height=\"20\"[^>]*>[^<]*<[a-z]+ [^>]*stroke=\"#0000ff\"[^>]*stroke-dasharray=\"3 3\"", html);
    }

    [Fact]
    [Trait("AC", "MEDIA-23")]
    public async Task StretchedImagesFillTheirBoxAsync()
    {
        var page = await fixture.CompileAsync("""
            <Grid ColumnDefinitions="*,*,*">
              <Border Height="120" />
              <Panel Grid.Column="1"><Image Source="avares://Aioros.Avalonia.Tests/Assets/dot.png" Stretch="UniformToFill" AutomationProperties.Name="Cover" /></Panel>
              <Panel Grid.Column="2"><Image Source="avares://Aioros.Avalonia.Tests/Assets/dot.png" Stretch="Fill" AutomationProperties.Name="Fill" /></Panel>
              <StackPanel Grid.Column="0"><Image Source="avares://Aioros.Avalonia.Tests/Assets/dot.png" Stretch="UniformToFill" AutomationProperties.Name="Stacked" /></StackPanel>
            </Grid>
            """);
        var html = EngineFixture.Body(page, [null]);

        string Rule(string name) => Regex.Match(page.Css, $"\\.{Regex.Match(html, $"alt=\"{name}\"[^>]* class=\"(k[0-9a-f]+)\"").Groups[1].Value}\\{{([^}}]*)\\}}").Groups[1].Value;

        Assert.Contains("width:100%;height:100%;object-fit:cover;", Rule("Cover"), StringComparison.Ordinal);
        Assert.Contains("width:100%;height:100%;object-fit:fill;", Rule("Fill"), StringComparison.Ordinal);
        Assert.Contains("max-width:100%;height:auto;object-fit:cover;", Rule("Stacked"), StringComparison.Ordinal);
    }
}
