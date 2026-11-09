using System.Collections.Immutable;
using Aioros.Runtime.Rendering;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class RangeControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "RANGES-01")]
    public async Task SlidersAreRangeInputsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Slider Minimum="0" Maximum="100" Value="{Binding Volume}" />
              <Slider Minimum="-5" Maximum="5" Value="2.5" TickFrequency="0.5" IsSnapToTickEnabled="True" IsEnabled="False" />
            </StackPanel>
            """, new Levels());

        var html = EngineFixture.Body(page, new Levels { Volume = 75 });
        Assert.Null(page.Failure);
        Assert.Matches("<input type=\"range\" name=\"f0\"(?: data-aioros-local=\"\")? min=\"0\" max=\"100\" step=\"any\" value=\"75\" class=\"[^\"]+\" style=\"--v:75\">", html);
        Assert.Matches("<input type=\"range\" name=\"f1\"(?: data-aioros-local=\"\")? min=\"-5\" max=\"5\" step=\"0.5\" value=\"2.5\" class=\"[^\"]+\" style=\"--v:2.5\" disabled>", html);
    }

    [Fact]
    [Trait("AC", "RANGES-02")]
    public async Task PostedNumbersMoveSlidersAsync()
    {
        var levels = new Levels();
        const string Body = """<Slider Minimum="0" Maximum="100" Value="{Binding Volume}" />""";

        await fixture.PostAsync(Body, levels, ("f0", "62.5"));
        var posted = levels.Volume;
        await fixture.PostAsync(Body, levels, ("f0", "loud"));
        await fixture.PostAsync(Body, levels);

        Assert.Equal(62.5, posted);
        Assert.Equal(62.5, levels.Volume);
    }

    [Fact]
    [Trait("AC", "RANGES-03")]
    public async Task SlidersWearTheFluentTrackAndThumbAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Slider Value="40" />
              <Slider Value="40" Orientation="Vertical" Height="160" />
            </StackPanel>
            """);

        Assert.Matches(@"height:50px;[^}]*--p:calc\(\(var\(--v,0\) - 0\) / 100 \* \(100% - 20px\) \+ 10px\);--f:#0078d7;--r:rgba\(0,0,0,0\.4\);--tb:#0078d7;", page.Css);
        Assert.Matches(@"::-webkit-slider-runnable-track\{height:2px;border-radius:1px;background:linear-gradient\(90deg,var\(--f\) 0 var\(--p\),var\(--r\) 0\)\}", page.Css);
        Assert.Matches(@"::-webkit-slider-thumb\{[^}]*width:20px;height:20px;border-radius:10px;background:var\(--tb\);", page.Css);
        Assert.Matches(@"::-moz-range-progress\{height:2px;border-radius:1px;background:var\(--f\)\}", page.Css);
        Assert.Matches(@"writing-mode:vertical-lr;direction:rtl;[^}]*\}[^\n]*::-webkit-slider-runnable-track\{width:2px;[^}]*linear-gradient\(0deg,", page.Css);
    }

    [Fact]
    [Trait("AC", "RANGES-04")]
    public async Task ProgressBarsAreProgressElementsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ProgressBar Value="{Binding Progress}" />
              <ProgressBar Minimum="10" Maximum="60" Value="35" ShowProgressText="True" />
            </StackPanel>
            """, new Levels());

        var html = EngineFixture.Body(page, new Levels { Progress = 80 });
        Assert.Null(page.Failure);
        Assert.Matches("<progress max=\"100\" value=\"80\" class=\"[^\"]+\"></progress>", html);
        Assert.Matches("<div class=\"[^\"]+\"><progress max=\"50\" value=\"25\" class=\"[^\"]+\"></progress><span>50%</span></div>", html);

        const string Text = """<ProgressBar Value="{Binding Progress}" ShowProgressText="True" />""";
        var texted = await fixture.CompileAsync(Text, new Levels());
        foreach (var progress in new[] { -1d, 140d })
        {
            var levels = new Levels { Progress = progress };
            Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Text, levels), levels), EngineFixture.Body(texted, levels));
        }

        Assert.Contains("value=\"0\" class=", EngineFixture.Body(texted, new Levels { Progress = -1 }), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-05")]
    public async Task IndeterminateProgressBarsAnimateAsync()
    {
        var page = await fixture.CompileAsync("""<ProgressBar IsIndeterminate="True" />""");

        Assert.Matches("^<progress class=\"[^\"]+\"></progress>$", EngineFixture.Body(page));
        Assert.Matches(@":indeterminate\{[^}]*animation:pbi 2s", page.Css);
        Assert.Contains("@keyframes pbi{", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-06")]
    public async Task CalendarsAreMonthTablesAsync()
    {
        var html = await fixture.HtmlAsync("""<Calendar DisplayDate="2024-03-14" SelectedDate="2024-03-14" />""");
        var bound = await fixture.CompileAsync("""<Calendar SelectedDate="{Binding Day}" />""", new Schedule());

        Assert.Matches("<div class=\"ch\"><span>March 2024</span><span class=\"cn\" aria-hidden=\"true\"><svg", html);
        Assert.Contains("<thead><tr><th>Su</th><th>Mo</th><th>Tu</th><th>We</th><th>Th</th><th>Fr</th><th>Sa</th></tr></thead>", html, StringComparison.Ordinal);
        Assert.Contains("<tbody><tr><td class=\"o\">25</td><td class=\"o\">26</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>13</td><td class=\"s\" aria-selected=\"true\">14</td>", html, StringComparison.Ordinal);
        Assert.Equal(42, Regex.Count(html, "<td"));
        Assert.Contains("Calendar", bound.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-07")]
    public async Task YearAndDecadeCalendarsAreTablesOfFourAsync()
    {
        var year = await fixture.HtmlAsync("""<Calendar DisplayMode="Year" DisplayDate="2024-03-14" SelectedDate="2024-03-14" />""");
        var decade = await fixture.HtmlAsync("""<Calendar DisplayMode="Decade" DisplayDate="2024-03-14" SelectedDate="2024-03-14" />""");

        Assert.Contains("<span>2024</span>", year, StringComparison.Ordinal);
        Assert.Contains("<tbody><tr><td>Jan</td><td>Feb</td><td class=\"s\" aria-selected=\"true\">Mar</td><td>Apr</td></tr><tr>", year, StringComparison.Ordinal);
        Assert.Equal(12, Regex.Count(year, "<td"));
        Assert.Contains("<span>2020-2029</span>", decade, StringComparison.Ordinal);
        Assert.Contains("<tbody><tr><td class=\"o\">2019</td><td>2020</td>", decade, StringComparison.Ordinal);
        Assert.Contains("<td class=\"s\" aria-selected=\"true\">2024</td>", decade, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-08")]
    public async Task CalendarDatePickersAreDateInputsAsync()
    {
        var page = await fixture.CompileAsync(
            """<CalendarDatePicker SelectedDate="{Binding Day}" DisplayDateStart="2024-01-01" DisplayDateEnd="2024-12-31" />""",
            new Schedule());

        Assert.Null(page.Failure);
        Assert.Matches("^<input type=\"date\" name=\"f0\" value=\"2024-03-14\" min=\"2024-01-01\" max=\"2024-12-31\" class=\"[^\"]+\">$", EngineFixture.Body(page, new Schedule()));
        Assert.Contains("value=\"\"", EngineFixture.Body(page, new Schedule { Day = null }), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-09")]
    public async Task PostedDatesSetCalendarDatePickersAsync()
    {
        var schedule = new Schedule();
        const string Body = """<CalendarDatePicker SelectedDate="{Binding Day}" />""";

        await fixture.PostAsync(Body, schedule, ("f0", "2024-05-02"));
        var posted = schedule.Day;
        await fixture.PostAsync(Body, schedule, ("f0", "soon"));
        await fixture.PostAsync(Body, schedule);
        var kept = schedule.Day;
        await fixture.PostAsync(Body, schedule, ("f0", string.Empty));

        Assert.Equal(new DateTime(2024, 5, 2, 0, 0, 0, DateTimeKind.Unspecified), posted);
        Assert.Equal(new DateTime(2024, 5, 2, 0, 0, 0, DateTimeKind.Unspecified), kept);
        Assert.Null(schedule.Day);
    }

    [Fact]
    [Trait("AC", "RANGES-10")]
    public async Task DatePickersAreDateInputsAsync()
    {
        var page = await fixture.CompileAsync(
            """<DatePicker SelectedDate="{Binding Start}" MinYear="2000-01-01T00:00:00Z" MaxYear="2030-01-01T00:00:00Z" />""",
            new Schedule());

        Assert.Null(page.Failure);
        Assert.Matches("^<input type=\"date\" name=\"f0\" value=\"2024-03-14\" min=\"2000-01-01\" max=\"2030-12-31\" class=\"[^\"]+\">$", EngineFixture.Body(page, new Schedule()));
    }

    [Fact]
    [Trait("AC", "RANGES-11")]
    public async Task PostedDatesSetDatePickersAsync()
    {
        var schedule = new Schedule();
        const string Body = """<DatePicker SelectedDate="{Binding Start}" />""";

        await fixture.PostAsync(Body, schedule, ("f0", "2024-05-02"));
        var posted = schedule.Start;
        await fixture.PostAsync(Body, schedule, ("f0", "soon"));
        await fixture.PostAsync(Body, schedule);
        var kept = schedule.Start;
        await fixture.PostAsync(Body, schedule, ("f0", string.Empty));

        Assert.Equal(new DateTimeOffset(2024, 5, 2, 0, 0, 0, TimeSpan.FromHours(2)), posted);
        Assert.Equal(TimeSpan.FromHours(2), posted!.Value.Offset);
        Assert.Equal(posted, kept);
        Assert.Null(schedule.Start);
    }

    [Fact]
    [Trait("AC", "RANGES-12")]
    public async Task TimePickersAreTimeInputsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TimePicker SelectedTime="{Binding At}" MinuteIncrement="15" />
              <TimePicker SelectedTime="08:05:30" UseSeconds="True" SecondIncrement="5" />
            </StackPanel>
            """, new Schedule());

        var html = EngineFixture.Body(page, new Schedule { At = new TimeSpan(9, 30, 0) });
        Assert.Null(page.Failure);
        Assert.Matches("<input type=\"time\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"09:30\" step=\"900\" class=\"[^\"]+\">", html);
        Assert.Matches("<input type=\"time\" name=\"f1\"(?: data-aioros-local=\"\")? value=\"08:05:30\" step=\"5\" class=\"[^\"]+\">", html);
    }

    [Fact]
    [Trait("AC", "RANGES-13")]
    public async Task PostedTimesSetTimePickersAsync()
    {
        var schedule = new Schedule();
        const string Body = """<TimePicker SelectedTime="{Binding At}" />""";

        await fixture.PostAsync(Body, schedule, ("f0", "08:30"));
        var minutes = schedule.At;
        await fixture.PostAsync(Body, schedule, ("f0", "08:30:15"));
        var seconds = schedule.At;
        await fixture.PostAsync(Body, schedule, ("f0", "late"));
        await fixture.PostAsync(Body, schedule);
        var kept = schedule.At;
        await fixture.PostAsync(Body, schedule, ("f0", string.Empty));

        Assert.Equal(new TimeSpan(8, 30, 0), minutes);
        Assert.Equal(new TimeSpan(8, 30, 15), seconds);
        Assert.Equal(seconds, kept);
        Assert.Null(schedule.At);
    }

    [Fact]
    [Trait("AC", "RANGES-14")]
    public async Task ScrollBarsAreTheBrowsersAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel Spacing="4">
              <ScrollBar Orientation="Horizontal" />
              <ScrollBar Orientation="Vertical" Height="80" />
            </StackPanel>
            """);

        Assert.Matches("^<div class=\"c k[0-9a-f]+\"></div>$", html);
    }

    [Fact]
    [Trait("AC", "RANGES-15")]
    public async Task SliderTicksAreDrawnAcrossTheTrackAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Slider Maximum="100" TickFrequency="25" TickPlacement="Outside" />
              <Slider Maximum="100" TickFrequency="10" TickPlacement="BottomRight" Orientation="Vertical" Height="160" />
            </StackPanel>
            """);

        Assert.Contains("background:repeating-linear-gradient(90deg,rgba(0,0,0,0.4) 0 1px,transparent 1px calc((100% - 1px) / 4)) 10px 7px/calc(100% - 19px) 4px no-repeat,repeating-linear-gradient(90deg,rgba(0,0,0,0.4) 0 1px,transparent 1px calc((100% - 1px) / 4)) 10px 39px/calc(100% - 19px) 4px no-repeat;", page.Css, StringComparison.Ordinal);
        Assert.Contains("background:repeating-linear-gradient(180deg,rgba(0,0,0,0.4) 0 1px,transparent 1px calc((100% - 1px) / 10)) 39px 10px/4px calc(100% - 19px) no-repeat;", page.Css, StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "RANGES-16")]
    public async Task SlidersDrawTheirThemesTrackAndThumbAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <x:Double x:Key="SliderTrackThemeHeight">6</x:Double>
                <ControlTheme x:Key="Part" TargetType="RepeatButton">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Background="Transparent" Margin="0,-5">
                        <Border Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Height="6" VerticalAlignment="Center" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Knob" TargetType="Thumb">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="ThumbBorder" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                              BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" BoxShadow="0 1 3 0 #1a000000" />
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:pointerover /template/ Border#ThumbBorder"><Setter Property="BoxShadow" Value="0 0 0 4 #33a1a1a1" /></Style>
                  <Style Selector="^.focused /template/ Border#ThumbBorder"><Setter Property="BoxShadow" Value="0 0 0 4 #33a1a1a1" /></Style>
                </ControlTheme>
                <ControlTheme x:Key="Range" TargetType="Slider">
                  <Setter Property="Background" Value="#f5f5f5" />
                  <Setter Property="Foreground" Value="#171717" />
                  <Setter Property="BorderBrush" Value="#171717" />
                  <Setter Property="Height" Value="6" />
                  <Setter Property="ClipToBounds" Value="False" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Panel>
                        <Track Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value, Mode=TwoWay}" Orientation="Horizontal">
                          <Track.DecreaseButton>
                            <RepeatButton Name="PART_DecreaseButton" Theme="{StaticResource Part}" Background="{TemplateBinding Foreground}" CornerRadius="3,0,0,3" Focusable="False" />
                          </Track.DecreaseButton>
                          <Track.IncreaseButton>
                            <RepeatButton Name="PART_IncreaseButton" Theme="{StaticResource Part}" Background="{TemplateBinding Background}" CornerRadius="0,3,3,0" Focusable="False" />
                          </Track.IncreaseButton>
                          <Thumb Name="thumb" Width="16" Height="16" Margin="0,-5" Background="White" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1"
                                 Theme="{StaticResource Knob}" Classes.focused="{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource TemplatedParent}}" />
                        </Track>
                      </Panel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Slider Theme="{StaticResource Range}" Value="50" />
            </StackPanel>
            """);

        var css = page.Css;
        Assert.Matches("\\.rg[0-9a-f]+\\{[^}]*height:6px;[^}]*--f:#171717;--r:#f5f5f5;--tb:#ffffff;--tc:#171717;--tw:1px;--ts:0px 1px 3px 0px rgba\\(0,0,0,0\\.102\\);", css);
        Assert.DoesNotMatch("\\.rg[0-9a-f]+\\{[^}]*overflow:hidden", css);
        Assert.Matches("::-webkit-slider-runnable-track\\{height:6px;border-radius:3px;", css);
        Assert.Matches("::-webkit-slider-thumb\\{[^}]*margin-block-start:-5px;box-sizing:border-box;width:16px;height:16px;border-radius:8px;", css);
        Assert.Matches("\\.rg[0-9a-f]+:hover\\{--ts:0px 0px 0px 4px rgba\\(161,161,161,0\\.2\\);\\}", css);
        Assert.Matches("\\.rg[0-9a-f]+:focus-visible\\{--ts:0px 0px 0px 4px rgba\\(161,161,161,0\\.2\\);\\}", css);
    }

    [Fact]
    [Trait("AC", "RANGES-30")]
    public async Task TwoSlidersMarkedAsARangeShareOnePanelAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Panel a:Web.Range="True">
                <Slider Maximum="1000" Value="200" />
                <Slider Maximum="1000" Value="800" />
              </Panel>
              <Panel>
                <Slider Value="10" />
                <Slider Value="20" />
              </Panel>
              <Panel a:Web.Range="True">
                <Slider Value="30" />
                <Slider Value="40" />
                <Slider Value="50" />
              </Panel>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Null(page.Failure);
        Assert.Matches("<div data-aioros-range class=\"[^\"]+\"><input type=\"range\" name=\"f0\"[^>]*value=\"200\"[^>]*><input type=\"range\" name=\"f1\"[^>]*value=\"800\"[^>]*><input type=\"hidden\" name=\"_range\" value=\"f0 f1\"></div>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-aioros-range"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "name=\"_range\""));
        Assert.Contains("[data-aioros-range]{touch-action:none}[data-aioros-range]>input[type=range]{pointer-events:none}[data-aioros-range]>input[type=range]::-webkit-slider-thumb{pointer-events:auto}[data-aioros-range]>input[type=range]::-moz-range-thumb{pointer-events:auto}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "RANGES-32")]
    public async Task ACrossedRangeIsBoundInOrderAsync()
    {
        const string Body = """
            <Panel a:Web.Range="True">
              <Slider Maximum="1000" Value="{Binding Low}" />
              <Slider Maximum="1000" Value="{Binding High}" />
            </Panel>
            """;
        var crossed = new Span();
        var ordered = new Span();
        var unmarked = new Span();

        await fixture.PostAsync(Body, crossed, [.. PlanForms.Ranges(Form(("f0", "900"), ("f1", "300"), ("_range", "f0 f1"))).Select(pair => (pair.Key, pair.Value))]);
        await fixture.PostAsync(Body, ordered, [.. PlanForms.Ranges(Form(("f0", "300"), ("f1", "900"), ("_range", "f0 f1"))).Select(pair => (pair.Key, pair.Value))]);
        await fixture.PostAsync(Body, unmarked, [.. PlanForms.Ranges(Form(("f0", "900"), ("f1", "300"))).Select(pair => (pair.Key, pair.Value))]);

        Assert.Equal((300.0, 900.0), (crossed.Low, crossed.High));
        Assert.Equal((300.0, 900.0), (ordered.Low, ordered.High));
        Assert.Equal((900.0, 300.0), (unmarked.Low, unmarked.High));
        Assert.False(PlanForms.Ranges(Form(("f0", "1"), ("_range", "f0 f1"))).ContainsKey("_range"));
    }

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);

    [Fact]
    [Trait("AC", "RANGES-33")]
    public async Task ABandInsetFromTheEndLeavesTheEndClearAsync()
    {
        const string Body = """
            <Panel Width="300" Height="40" HorizontalAlignment="Left" VerticalAlignment="Top" Background="White">
              <Panel.Resources>
                <ControlTheme x:Key="Band" TargetType="RepeatButton">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Background="Transparent" Margin="0,-5">
                        <Border Background="{TemplateBinding Background}" Margin="{TemplateBinding Padding}" CornerRadius="{TemplateBinding CornerRadius}" Height="6" VerticalAlignment="Center" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
                <ControlTheme x:Key="Inset" TargetType="Slider">
                  <Setter Property="Height" Value="6" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Track Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value, Mode=TwoWay}" Orientation="Horizontal">
                        <Track.DecreaseButton><RepeatButton Name="PART_DecreaseButton" Theme="{StaticResource Band}" Background="#0000ff" CornerRadius="3,0,0,3" /></Track.DecreaseButton>
                        <Track.IncreaseButton><RepeatButton Name="PART_IncreaseButton" Theme="{StaticResource Band}" Background="#ff0000" Padding="0,0,3,0" CornerRadius="0,3,3,0" /></Track.IncreaseButton>
                        <Thumb Width="16" Height="16" Margin="0,-5"><Thumb.Template><ControlTemplate><Border Background="White" BorderBrush="Black" BorderThickness="1" CornerRadius="8" /></ControlTemplate></Thumb.Template></Thumb>
                      </Track>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </Panel.Resources>
              <Slider Theme="{StaticResource Inset}" Value="20" VerticalAlignment="Center" />
            </Panel>
            """;
        var page = await fixture.CompileAsync(Body);
        var web = SkiaSharp.SKBitmap.Decode(await BrowserLayout.ScreenshotAsync(page, 300, 40));
        var desktop = SkiaSharp.SKBitmap.Decode((await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 40))).Png);
        string At(SkiaSharp.SKBitmap bitmap, int x) => bitmap.GetPixel(x, 20) is var pixel && pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200 ? "clear" : pixel.Red > 200 && pixel.Green < 80 ? "band" : pixel.ToString();

        Assert.True(page.Css.Contains("linear-gradient(90deg,var(--f) 0 var(--p),var(--r) 0 calc(100% - 3px),transparent 0)", StringComparison.Ordinal), page.Css[page.Css.IndexOf("slider-runnable-track", StringComparison.Ordinal)..][..300]);
        Assert.Equal(("band", "clear", "clear"), (At(desktop, 290), At(desktop, 298), At(desktop, 299)));
        Assert.Equal(("band", "clear", "clear"), (At(web, 290), At(web, 298), At(web, 299)));
    }
}

