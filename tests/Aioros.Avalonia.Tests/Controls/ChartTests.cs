using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Charts;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class ChartTests(EngineFixture fixture)
{
    private const string Trend = """
        <a:Chart Title="{Binding Title}" Height="200" Width="400">
          <a:ChartSeries Kind="Area" ItemsSource="{Binding Prices}" XPath="Day" YPath="Value" Stroke="#5E6AD2" />
          <a:ChartSeries Kind="Line" ItemsSource="{Binding Target}" XPath="Day" YPath="Value" Stroke="#FF0000" />
        </a:Chart>
        """;

    [Fact]
    [Trait("AC", "CHARTS-01")]
    public async Task BoundSeriesFollowTheModelAsync()
    {
        var page = await fixture.CompileAsync(Trend, new Market());
        var rising = EngineFixture.Body(page, new Market { Target = Market.Series(10, 20, 30, 40) });
        var falling = EngineFixture.Body(page, new Market { Target = Market.Series(40, 30, 20, 10) });

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("<svg", rising, StringComparison.Ordinal);
        Assert.Single(StrokeLines(rising, "#5e6ad2"));
        Assert.Single(Fills(rising, "#5e6ad2"));
        var up = Ys(Assert.Single(StrokeLines(rising, "#ff0000")));
        var down = Ys(Assert.Single(StrokeLines(falling, "#ff0000")));
        Assert.Equal(4, up.Count);
        Assert.True(up.Zip(up.Skip(1)).All(pair => pair.Second < pair.First), "rising values go up the screen");
        Assert.True(down.Zip(down.Skip(1)).All(pair => pair.Second > pair.First), "falling values go down the screen");
    }

    [Fact]
    [Trait("AC", "CHARTS-02")]
    public async Task ChartsScaleAndKeepIdsApartAsync()
    {
        var page = await fixture.CompileAsync($"<StackPanel>{Trend}{Trend.Replace("Height=\"200\"", "Height=\"120\"", StringComparison.Ordinal)}</StackPanel>", new Market());
        var html = EngineFixture.Body(page, new Market { Title = "Sales" });

        Assert.Equal(2, Regex.Count(html, "<div class=\"[^\"]+\" role=\"img\" aria-label=\"Sales\"><svg viewBox=\"[0-9.]+ 0 [0-9.]+ (200|120)\" preserveAspectRatio=\"none\""));
        Assert.Equal(2, Regex.Count(html, "style=\"position:absolute;top:0;left:[0-9.]+px;width:calc\\(100% - [0-9.]+px\\)"));
        Assert.Contains("vector-effect=\"non-scaling-stroke\"", html, StringComparison.Ordinal);
        Assert.Matches("<span class=\"cl cr\" style=\"left:[0-9.]+px;", html);
        Assert.Matches("<span class=\"cl cc\" style=\"left:calc\\([0-9.]+px \\+ \\(100% - [0-9.]+px\\) \\* [0-9.]+\\);", html);
        var ids = ClipId().Matches(html).Select(match => match.Groups[1].Value).ToList();
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("AC", "CHARTS-03")]
    public async Task SvgIsReusedForTheSameDataAsync()
    {
        var page = await fixture.CompileAsync(Trend, new Market());
        var market = new Market();
        var before = ChartSvg.Built;

        var first = EngineFixture.Body(page, market);
        var second = EngineFixture.Body(page, market);
        var afterSame = ChartSvg.Built;
        _ = EngineFixture.Body(page, new Market());

        Assert.Equal(first, second);
        Assert.Equal(before + 1, afterSame);
        Assert.Equal(afterSame + 1, ChartSvg.Built);
    }

    [Fact]
    [Trait("AC", "CHARTS-11")]
    public async Task SvgFollowsAListChangedInPlaceAsync()
    {
        const string Live = """
            <a:Chart Height="200" Width="400"><a:ChartSeries Kind="Line" ItemsSource="{Binding Prices}" XPath="Day" YPath="Value" Stroke="#FF0000" /></a:Chart>
            """;
        var market = new LiveMarket();
        var page = await fixture.CompileAsync(Live, market);
        var planned = Steps(market, () => EngineFixture.Body(page, market));
        var other = new LiveMarket();
        var definition = new ChartDefinition(null, "#000000", string.Empty, true, 400, 200, [new SeriesDefinition(ChartKind.Line, "Prices", "Day", "Value", null, null, null, "#ff0000", "#ff0000", 1)]);
        var engine = Steps(other, () => ChartSvg.Draw(definition, definition.Json(), [other.Prices]));

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal((0, 1, 1, 4, 5, 5), planned);
        Assert.Equal((0, 1, 1, 4, 5, 5), engine);
    }

    private static (int Unchanged, int Added, int Moved, int Before, int After, int Points) Steps(LiveMarket market, Func<string> render)
    {
        var first = render();
        var built = ChartSvg.Built;
        var again = render();
        var unchanged = ChartSvg.Built - built;
        market.Prices.Add(new Reading { Day = market.Prices[^1].Day.AddDays(1), Value = 25 });
        var added = render();
        var addedBuilt = ChartSvg.Built - built - unchanged;
        market.Prices[0].Value = 45;
        var moved = render();
        var movedBuilt = ChartSvg.Built - built - unchanged - addedBuilt;
        Assert.Equal(first, again);
        Assert.NotEqual(Ys(Assert.Single(StrokeLines(added, "#ff0000")))[0], Ys(Assert.Single(StrokeLines(moved, "#ff0000")))[0]);
        return (unchanged, addedBuilt, movedBuilt, Ys(Assert.Single(StrokeLines(first, "#ff0000"))).Count, Ys(Assert.Single(StrokeLines(added, "#ff0000"))).Count, Ys(Assert.Single(StrokeLines(moved, "#ff0000"))).Count);
    }

    [Fact]
    [Trait("AC", "CHARTS-04")]
    public async Task ColumnsCandlesAndDonutsAreDrawnAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <a:Chart Height="200" Width="400"><a:ChartSeries Kind="Column" ItemsSource="{Binding Sales}" XPath="Region" YPath="Amount" Fill="#00AA00" /></a:Chart>
              <a:Chart Height="200" Width="400"><a:ChartSeries Kind="Candle" ItemsSource="{Binding Candles}" XPath="Day" OpenPath="Open" HighPath="High" LowPath="Low" YPath="Close" Fill="#00AA00" Stroke="#AA0000" /></a:Chart>
              <a:Chart Height="200" Width="200"><a:ChartSeries Kind="Donut" ItemsSource="{Binding Shares}" XPath="Name" YPath="Part" /></a:Chart>
            </StackPanel>
            """, new Market());
        var html = EngineFixture.Body(page, new Market());
        var charts = Regex.Matches(html, "<svg.*?</svg>").Select(match => match.Value).ToList();

        var bars = Regex.Matches(charts[0], "<rect[^>]*height=\"([0-9.]+)\"[^>]*fill=\"#00aa00\"").Select(match => double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(3, bars.Count);
        Assert.Equal(2, bars[1] / bars[0], 1);
        Assert.Equal(4, bars[2] / bars[0], 1);
        Assert.Equal(2, Regex.Count(charts[1], "fill=\"#00aa00\""));
        Assert.Single(Regex.Matches(charts[1], "fill=\"#aa0000\""));
        Assert.Equal(3, Regex.Count(charts[2], "<polygon"));
    }

    [Fact]
    [Trait("AC", "CHARTS-05")]
    public async Task ChartsDrawNativelyAsync()
    {
        var drawn = await fixture.CaptureAsync("""
            <a:Chart Width="200" Height="120" HorizontalAlignment="Left" VerticalAlignment="Top">
              <a:ChartSeries Kind="Line" ItemsSource="{Binding Prices}" XPath="Day" YPath="Value" Stroke="#FF0000" StrokeThickness="4" />
            </a:Chart>
            """, new Market(), width: 240, height: 160);

        Assert.True(Pixels.Red(drawn) > 200);
    }

    [Fact]
    [Trait("AC", "CHARTS-06")]
    public async Task ThemeColorsFollowTheThemeAsync()
    {
        const string themed = """
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary>
                  <ResourceDictionary.ThemeDictionaries>
                    <ResourceDictionary x:Key="Light"><SolidColorBrush x:Key="Accent" Color="#112233" /></ResourceDictionary>
                    <ResourceDictionary x:Key="Dark"><SolidColorBrush x:Key="Accent" Color="#AABBCC" /></ResourceDictionary>
                  </ResourceDictionary.ThemeDictionaries>
                </ResourceDictionary>
              </StackPanel.Resources>
              <a:Chart Height="100" Width="200"><a:ChartSeries Kind="Line" ItemsSource="{Binding Prices}" XPath="Day" YPath="Value" Stroke="{DynamicResource Accent}" /></a:Chart>
            </StackPanel>
            """;
        var light = EngineFixture.Body(await fixture.CompileAsync(themed, new Market()), new Market());
        var dark = EngineFixture.Body(await fixture.CompileAsync(themed, new Market(), "dark"), new Market());

        Assert.Single(StrokeLines(light, "#112233"));
        Assert.Single(StrokeLines(dark, "#aabbcc"));
    }

    [Fact]
    [Trait("AC", "CHARTS-07")]
    public async Task ChartPlansSerializeAsync()
    {
        var page = await fixture.CompileAsync(Trend, new Market());
        var write = PlanSerializer.TryWrite(page.Plan.Body);
        var read = PlanSerializer.Read(write.Json ?? "[]", new AdapterSlotResolver());
        var market = new Market { Title = "Other" };

        Assert.Null(write.Failure);
        Assert.Equal(EngineFixture.Body(page, market), Render(read, market));
    }

    [Fact]
    [Trait("AC", "CHARTS-08")]
    public void TextIsMeasuredAlikeOnBothTargets()
    {
        const string label = "Jan 12, 2026";

        var web = new SvgRenderContext("t").MeasureText(label, "Inter", 11, 400);
        var desktop = AvaloniaRenderContext.Measure(label, "Inter", 11, 400);

        Assert.Equal(InterMetrics.Width(label, 11, bold: false), web.Width, 3);
        Assert.Equal(web, desktop);
    }

    [Fact]
    [Trait("AC", "CHARTS-09")]
    public async Task ChartErrorsFailTheRenderAsync()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() => fixture.CompileAsync("""
            <a:Chart Height="100" Width="200">
              <a:ChartSeries Kind="Line" ItemsSource="{Binding Prices}" XPath="Day" YPath="Value" />
              <a:ChartSeries Kind="Column" ItemsSource="{Binding Sales}" XPath="Region" YPath="Amount" />
            </a:Chart>
            """, new Market()));

        Assert.Contains("The chart could not be laid out", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "CHARTS-10")]
    public async Task AxesFitTheDataAsync()
    {
        var day = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);
        var intraday = new Market { Prices = [.. Enumerable.Range(0, 60).Select(minute => new Price(day.AddMinutes(minute * 5), 180 + (minute % 7)))] };
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <a:Chart Height="200" Width="400"><a:ChartSeries Kind="Area" ItemsSource="{Binding Prices}" XPath="Day" YPath="Value" /></a:Chart>
              <a:Chart Height="160"><a:ChartSeries Kind="Donut" ItemsSource="{Binding Shares}" XPath="Name" YPath="Part" /></a:Chart>
            </StackPanel>
            """, intraday);
        var html = EngineFixture.Body(page, intraday);
        var labels = Regex.Matches(html, "<span class=\"cl[^\"]*\"[^>]*>([^<]+)</span>").Select(match => match.Groups[1].Value).ToList();

        Assert.DoesNotContain("0", labels);
        Assert.Contains(labels, label => Regex.IsMatch(label, @"^\d\d:\d\d$"));
        Assert.Contains("viewBox=\"0 0 160 160\"", html, StringComparison.Ordinal);
    }

    private static List<string> StrokeLines(string html, string color) =>
        [.. Regex.Matches(html, $"<polyline points=\"([^\"]+)\"[^>]*stroke=\"{color}\"").Select(match => match.Groups[1].Value)];

    private static List<string> Fills(string html, string color) =>
        [.. Regex.Matches(html, $"<polygon[^>]*fill=\"{color}\"").Select(match => match.Value)];

    private static List<double> Ys(string points) =>
        [.. points.Split(' ').Select(point => double.Parse(point.Split(',')[1], System.Globalization.CultureInfo.InvariantCulture))];

    private static string Render(System.Collections.Immutable.ImmutableArray<IPlanNode> body, object model)
    {
        var writer = new ArrayBufferWriter<byte>();
        new RenderPlan([], body, []).RenderBody(writer, [model]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    [GeneratedRegex("<clipPath id=\"([^\"]+)\"")]
    private static partial Regex ClipId();
}
