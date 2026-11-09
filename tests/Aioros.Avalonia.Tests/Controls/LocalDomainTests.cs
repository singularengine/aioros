using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Testing.Domains;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class LocalDomainTests(EngineFixture fixture)
{
    private static readonly string[] Types =
    [
        """<CheckBox Content="Remember" />""",
        """<CheckBox Content="Maybe" IsThreeState="True" />""",
        """<ToggleSwitch OnContent="Shown on every page" OffContent="No" AutomationProperties.Name="Switch" />""",
        """<ToggleButton Content="Bold" />""",
        """<ToggleButton AutomationProperties.Name="Like"><a:Icon Kind="heart" Size="16" /></ToggleButton>""",
        """<ToggleSplitButton Content="Align" />""",
        """<ListBox><x:String>One</x:String><x:String>Two</x:String><x:String>Three</x:String></ListBox>""",
        """<ComboBox Width="200"><x:String>Ada</x:String><x:String>Bea</x:String><x:String>Cyd</x:String></ComboBox>""",
        """<ComboBox Width="200" PlaceholderText="Pick one" HorizontalContentAlignment="Left"><x:String>Ada</x:String><x:String>Beatrice Longname</x:String><x:String>Cyd</x:String></ComboBox>""",
        """<ComboBox><x:String>Beta</x:String><x:String>A much longer item</x:String></ComboBox>""",
        """<TabControl><TabItem Header="One"><TextBlock Text="First" /></TabItem><TabItem Header="Two"><TextBlock Text="Second" /></TabItem><TabItem Header="Three"><TextBlock Text="Third" /></TabItem></TabControl>""",
        """<Slider Minimum="0" Maximum="10" Value="4" Width="200" AutomationProperties.Name="Volume" />""",
        """<TextBox Text="demo" Width="200" AutomationProperties.Name="Demo" />""",
        """<AutoCompleteBox Text="Ad" Width="200" AutomationProperties.Name="Name" />""",
        """<NumericUpDown Value="3" Minimum="0" Maximum="9" Width="200" AutomationProperties.Name="Count" />""",
        """<CalendarDatePicker Width="240" DisplayDate="2025-06-12" AutomationProperties.Name="Due" />""",
        """<DatePicker AutomationProperties.Name="Day" />""",
        """<TimePicker AutomationProperties.Name="At" />""",
        """<PipsPager NumberOfPages="5" />""",
    ];

    [Fact]
    [Trait("AC", "ENGINE-322")]
    public async Task EachLocalInputTypeRendersEveryValueOfItsDomainAsAvaloniaDoesAsync()
    {
        var failures = new List<string>();
        foreach (var type in Types)
        {
            failures.AddRange((await DisagreementsAsync(type, static () => [new Profile()])).Select(failure => $"{type}: {failure}"));
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    [Trait("AC", "ENGINE-324")]
    public async Task InputsThatFollowOneAnotherRenderEveryValueAndPairAsAvaloniaDoesAsync()
    {
        string[] pages =
        [
            """<StackPanel><CheckBox Name="a" Content="Enable" /><CheckBox Content="Second" IsEnabled="{Binding #a.IsChecked}" /></StackPanel>""",
            """<StackPanel><ToggleSwitch Name="a" AutomationProperties.Name="Lock" /><TextBox Text="demo" Width="200" IsReadOnly="{Binding #a.IsChecked}" AutomationProperties.Name="T" /></StackPanel>""",
            """<StackPanel><CheckBox Name="a" Content="Show" /><ToggleButton Content="Bold" IsVisible="{Binding #a.IsChecked}" /></StackPanel>""",
            """<StackPanel><TextBox Name="t" Text="x" Width="200" AutomationProperties.Name="T" /><TextBlock Text="{Binding #t.Text}" /><CheckBox Content="Other" /></StackPanel>""",
            """<StackPanel><Slider Name="s" Minimum="0" Maximum="10" Value="4" Width="200" AutomationProperties.Name="S" /><ProgressBar Value="{Binding #s.Value}" Maximum="10" Width="200" /><CheckBox Content="Other" /></StackPanel>""",
            """<StackPanel><CheckBox Content="A" /><CheckBox Content="B" /><CheckBox Content="C" /></StackPanel>""",
            """<StackPanel><CheckBox Name="a" Content="Pick" /><ComboBox Width="200" IsEnabled="{Binding #a.IsChecked}"><x:String>Ada</x:String><x:String>Bea</x:String></ComboBox></StackPanel>""",
            """<StackPanel><CheckBox Name="a" Content="Hide" /><Slider Width="200" Minimum="0" Maximum="10" Value="3" IsEnabled="{Binding !#a.IsChecked}" AutomationProperties.Name="S" /></StackPanel>""",
            """<StackPanel><CheckBox Name="a" Content="A" /><CheckBox Name="b" Content="B" IsEnabled="{Binding #a.IsChecked}" /><CheckBox Content="C" IsEnabled="{Binding #b.IsChecked}" /></StackPanel>""",
            """<StackPanel><CheckBox Name="a" Content="Hide" /><ListBox IsVisible="{Binding !#a.IsChecked}"><x:String>One</x:String><x:String>Two</x:String></ListBox><Slider Minimum="0" Maximum="10" Value="4" Width="200" AutomationProperties.Name="S" /></StackPanel>""",
            """<StackPanel><CheckBox Name="a" Content="Lock" /><CheckBox Content="Locked" IsEnabled="{Binding !#a.IsChecked}" /></StackPanel>""",
            """<StackPanel><ToggleSwitch Name="a" /><ToggleSwitch IsEnabled="{Binding #a.IsChecked}" /><Slider IsVisible="{Binding #a.IsChecked}" /></StackPanel>""",
        ];
        var failures = new List<string>();
        foreach (var page in pages)
        {
            failures.AddRange((await DisagreementsAsync(page, static () => [new Profile()])).Select(failure => $"{page}: {failure}"));
        }

        var watermark = await DisagreementsAsync("""<StackPanel><CheckBox Name="a" Content="Upper" /><TextBox Text="demo" Width="200" AutomationProperties.Name="T" Watermark="{Binding #a.IsChecked}" /></StackPanel>""", static () => [new Profile()]);

        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.True(watermark is [var refused] && refused.StartsWith("not reusable: ", StringComparison.Ordinal), string.Join("\n", watermark));
    }

    [Fact]
    [Trait("AC", "ENGINE-326")]
    public async Task AStateStyleOnTheControlItselfRendersEveryValueAsAvaloniaDoesAsync()
    {
        string[] setters = ["""<Setter Property="Foreground" Value="#C2185B" />""", """<Setter Property="FontWeight" Value="Bold" />""", """<Setter Property="FontSize" Value="20" />""", """<Setter Property="Opacity" Value="0.5" />""", """<Setter Property="Margin" Value="20" />""", """<Setter Property="Padding" Value="10,4" />"""];
        string[] inputs = ["""CheckBox|<CheckBox Content="Remember" />""", """CheckBox|<CheckBox Content="Maybe" IsThreeState="True" />""", """ToggleSwitch|<ToggleSwitch AutomationProperties.Name="Switch" />""", """ToggleButton|<ToggleButton Content="Bold" />"""];
        var failures = new List<string>();
        foreach (var (type, input) in inputs.Select(static entry => (entry.Split('|')[0], entry.Split('|')[1])))
        {
            foreach (var setter in setters.Where(setter => type != "CheckBox" || !setter.Contains("Padding", StringComparison.Ordinal)))
            {
                var page = $"""<StackPanel><StackPanel.Styles><Style Selector="{type}:checked">{setter}</Style></StackPanel.Styles>{input}</StackPanel>""";
                failures.AddRange((await DisagreementsAsync(page, static () => [new Profile()])).Select(failure => $"{type} {setter}: {failure}"));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    [Trait("AC", "ENGINE-330")]
    public async Task AStyleSelectingAnInputByItsValueRendersEveryValueAsAvaloniaDoesAsync()
    {
        string[] pages =
        [
            """<StackPanel><StackPanel.Styles><Style Selector="CheckBox[IsChecked=True]"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles><CheckBox Content="Remember" /></StackPanel>""",
            """<StackPanel><StackPanel.Styles><Style Selector="CheckBox[IsChecked=False]"><Setter Property="Opacity" Value="0.6" /></Style></StackPanel.Styles><CheckBox Content="Remember" /></StackPanel>""",
            """<StackPanel><StackPanel.Styles><Style Selector="ToggleSwitch[IsChecked=True]"><Setter Property="Foreground" Value="#C2185B" /></Style></StackPanel.Styles><ToggleSwitch AutomationProperties.Name="Switch" /></StackPanel>""",
            """<StackPanel><StackPanel.Styles><Style Selector="ToggleButton[IsChecked=True]"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles><ToggleButton Content="Bold" /></StackPanel>""",
        ];
        var failures = new List<string>();
        foreach (var page in pages)
        {
            failures.AddRange((await DisagreementsAsync(page, static () => [new Profile()])).Select(failure => $"{page}: {failure}"));
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    [Trait("AC", "ENGINE-323")]
    public async Task TheDomainWalkReportsWhatItCannotCloseAsync()
    {
        var renders = 0;
        var reports = new[]
        {
            await DisagreementsAsync("""<CheckBox Content="{Binding Name}" />""", () => [new Profile { Name = "Ada" + renders++ }]),
            await DisagreementsAsync("""<TextBlock Text="{Binding Name}" />""", static () => [new Profile()]),
            await DisagreementsAsync("""<StackPanel><StackPanel.KeyBindings><KeyBinding Gesture="Ctrl+Q" Command="{x:Static m:KeyCommands.Quit}" /></StackPanel.KeyBindings><CheckBox /></StackPanel>""", static () => [new Profile()]),
        };

        Assert.Equal(
            [true, true, true],
            [
                reports[0] is [var first, var second] && first.StartsWith("check=on: differs at ", StringComparison.Ordinal) && second.StartsWith("check=(none): differs at ", StringComparison.Ordinal),
                reports[1] is ["holds no local input"],
                reports[2] is [var refused] && refused.StartsWith("not reusable: ", StringComparison.Ordinal),
            ]);
    }

    private async Task<IReadOnlyList<string>> DisagreementsAsync(string body, Func<ImmutableArray<object?>> models) =>
        await LocalDomains.DisagreementsAsync(fixture.Engine, ControlHarness.Xaml(body, nameof(Profile)), models, "light", TestContext.Current.CancellationToken);
}
