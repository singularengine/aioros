using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class CarouselTests(EngineFixture fixture)
{
    private const string Theme = """
        <UserControl.Resources>
          <ControlTheme x:Key="Slides" TargetType="Carousel">
            <Setter Property="Template">
              <ControlTemplate>
                <DockPanel>
                  <StackPanel Name="PART_Indicators" DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8">
                    <Button Classes="dot" /><Button Classes="dot on" /><Button Classes="dot" />
                  </StackPanel>
                  <Panel>
                    <ScrollViewer Name="PART_ScrollViewer"><ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" /></ScrollViewer>
                    <Button Name="PART_Previous" HorizontalAlignment="Left" Content="Prev" />
                    <Button Name="PART_Next" HorizontalAlignment="Right" Content="Next" />
                  </Panel>
                </DockPanel>
              </ControlTemplate>
            </Setter>
            <Style Selector="^ /template/ Button.dot">
              <Setter Property="Background" Value="Gray" />
              <Setter Property="Width" Value="8" />
              <Setter Property="Height" Value="8" />
            </Style>
            <Style Selector="^ /template/ Button.on">
              <Setter Property="Background" Value="Blue" />
            </Style>
          </ControlTheme>
        </UserControl.Resources>
        """;

    [Fact]
    [Trait("AC", "ITEMS-103")]
    public async Task CarouselsAreWrittenThroughTheirTemplateAsync()
    {
        var page = await fixture.CompileAsync(Theme + """
            <StackPanel>
              <Carousel Theme="{StaticResource Slides}" SelectedIndex="1" WrapSelection="True" Height="100">
                <TextBlock Text="First" /><TextBlock Text="Second" /><TextBlock Text="Third" />
              </Carousel>
              <Carousel Height="200" ViewportFraction="0.5">
                <Carousel.PageTransition><PageSlide Orientation="Vertical" /></Carousel.PageTransition>
                <TextBlock Text="Up" /><TextBlock Text="Down" />
              </Carousel>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);
        var dots = Regex.Match(html, "<button type=\"button\" data-aioros-slide-to class=\"([^\"]+)\"></button><button type=\"button\" data-aioros-slide-to aria-current=\"true\" class=\"([^\"]+)\"></button><button type=\"button\" data-aioros-slide-to class=\"([^\"]+)\"></button>");
        var vertical = Regex.Match(html, "<div data-aioros-carousel=\"v\" data-aioros-strip role=\"region\" aria-roledescription=\"carousel\" class=\"([^\"]+)\"><div role=\"group\" aria-roledescription=\"slide\" class=\"c cs sel (cf[0-9a-f]+)\">");

        Assert.Matches("<div data-aioros-carousel data-aioros-wrap role=\"region\" aria-roledescription=\"carousel\" class=\"[^\"]+\">", html);
        Assert.Matches("<div data-aioros-strip class=\"[^\"]+\"><div role=\"group\" aria-roledescription=\"slide\" class=\"c cs\"><div[^>]*>First</div></div><div role=\"group\" aria-roledescription=\"slide\" class=\"c cs sel\">", html);
        Assert.Matches("<button type=\"button\" data-aioros-slide=\"-1\" class=\"[^\"]+\">Prev</button>", html);
        Assert.Matches("<button type=\"button\" data-aioros-slide=\"1\" class=\"[^\"]+\">Next</button>", html);
        Assert.True(dots.Success, html);
        Assert.Equal(dots.Groups[1].Value, dots.Groups[3].Value);
        Assert.NotEqual(dots.Groups[1].Value, dots.Groups[2].Value);
        Assert.True(vertical.Success, html);
        Assert.Contains("flex-direction:column;overflow-y:auto;scroll-snap-type:y mandatory;", page.Css, StringComparison.Ordinal);
        Assert.Contains($".{vertical.Groups[2].Value}{{flex-basis:50%;scroll-snap-align:center}}", page.Css, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, "data-aioros-slide=\""));
    }

    [Fact]
    [Trait("AC", "ITEMS-104")]
    public async Task BoundCarouselsPostTheirSelectedSlideAsync()
    {
        const string Body = Theme + """
            <Carousel Theme="{StaticResource Slides}" ItemsSource="{Binding Items}" SelectedIndex="{Binding Index}" Height="100">
              <Carousel.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></Carousel.ItemTemplate>
            </Carousel>
            """;
        static Leaves Of(int count, int index) => new([.. Enumerable.Range(1, count).Select(at => new Item("s" + at))], index);

        var page = await fixture.PlansLikeAvaloniaAsync(Body, Of(3, 1), Of(2, 0), Of(3, 2));
        var html = EngineFixture.Body(page, Of(3, 1));
        var (planned, engine) = (Of(3, 0), Of(3, 0));
        await PlanForms.ApplyAsync(page.Plan, [planned], ImmutableDictionary<string, string>.Empty.Add("f0", "2"), new AdapterSlotResolver());
        await fixture.PostAsync(Body, engine, ("f0", "2"));

        Assert.Matches("<div data-aioros-carousel role=\"region\" aria-roledescription=\"carousel\" data-change class=", html);
        Assert.Matches("<div role=\"group\" aria-roledescription=\"slide\" class=\"c cs sel\"><input type=\"hidden\" name=\"f0\" value=\"1\"><div[^>]*>s2</div></div>", html);
        Assert.Equal(3, Regex.Count(html, "data-aioros-slide-to"));
        Assert.Equal(2, Regex.Count(EngineFixture.Body(page, Of(2, 0)), "data-aioros-slide-to"));
        Assert.Equal((2, 2), (planned.Index, engine.Index));
    }

    [Fact]
    [Trait("AC", "ITEMS-107")]
    public async Task AspectRatiosKeepTheirSlidesHeightAsync()
    {
        var page = await fixture.CompileAsync("""
            <Carousel Width="300">
              <Border a:Web.AspectRatio="2" Background="Red"><TextBlock Text="Wide" /></Border>
            </Carousel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.Matches("aria-roledescription=\"slide\" class=\"c cs sel\"><div class=\"[^\"]+\"><div[^>]*>Wide</div></div>", html);
        Assert.Contains("aspect-ratio:2;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-118")]
    public async Task VerticalStripsTakeTheirLaidOutHeightAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Carousel Height="196" ViewportFraction="0.7449">
                <Carousel.PageTransition><PageSlide Orientation="Vertical" /></Carousel.PageTransition>
                <Carousel.Template>
                  <ControlTemplate>
                    <DockPanel>
                      <Button Name="PART_Previous" DockPanel.Dock="Top" Height="24" Content="Up" />
                      <Button Name="PART_Next" DockPanel.Dock="Bottom" Height="24" Content="Down" />
                      <ScrollViewer Name="PART_ScrollViewer"><ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" /></ScrollViewer>
                    </DockPanel>
                  </ControlTemplate>
                </Carousel.Template>
                <Border Height="400"><TextBlock Text="One" /></Border>
                <Border Height="400"><TextBlock Text="Two" /></Border>
              </Carousel>
              <Carousel Height="120"><Border><TextBlock Text="Wide" /></Border></Carousel>
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);
        var vertical = html[html.IndexOf("data-aioros-carousel=\"v\"", StringComparison.Ordinal)..html.IndexOf(">One<", StringComparison.Ordinal)];
        var horizontal = html[html.LastIndexOf("data-aioros-carousel", StringComparison.Ordinal)..];

        Assert.Matches("data-aioros-strip class=\"[^\"]*\\bcsf\\b", vertical);
        Assert.True(Regex.Count(vertical, "class=\"[^\"]*\\bcsf\\b") >= 2);
        Assert.DoesNotMatch("\\bcsf\\b", horizontal);
        Assert.Contains(".csf{min-height:0!important}.c>.csf{flex:1 1 0!important}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-145")]
    public async Task ASwipeableCarouselIsDraggedByTheMouseAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Carousel IsSwipeEnabled="True"><TextBlock Text="A" /><TextBlock Text="B" /></Carousel>
              <Carousel IsSwipeEnabled="False"><TextBlock Text="C" /><TextBlock Text="D" /></Carousel>
            </StackPanel>
            """);
        var carousels = Regex.Matches(html, "<div data-aioros-carousel[^>]*>").Select(match => match.Value).ToList();

        Assert.Equal(2, carousels.Count);
        Assert.Contains(" data-aioros-drag", carousels[0], StringComparison.Ordinal);
        Assert.DoesNotContain(" data-aioros-drag", carousels[1], StringComparison.Ordinal);
    }
}
