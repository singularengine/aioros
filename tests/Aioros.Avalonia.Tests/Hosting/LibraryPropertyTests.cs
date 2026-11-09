using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class LibraryPropertyTests(EngineFixture fixture)
{
    private static string Themed(string body) => $"""
        <StackPanel>
          <StackPanel.Resources>
            <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CalloutTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
          </StackPanel.Resources>
          {body}
        </StackPanel>
        """;

    private const string Items = """<ItemsControl ItemsSource="{Binding Items}"><ItemsControl.ItemTemplate><DataTemplate x:DataType="m:Bulletin"><StackPanel><m:Callout Title="{Binding Title}" Description="{Binding Description}" /><m:Portrait Content="{Binding Initials}" /></StackPanel></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>""";

    [Fact]
    [Trait("AC", "ENGINE-218")]
    public async Task ShownLibraryPropertiesAreSlotsAsync()
    {
        var body = Themed("""<m:Callout Title="{Binding Title}" Description="{Binding Description}" />""");

        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Bulletin("First", "One"), new Bulletin("Second", "Two"), new Bulletin("Third"));

        Assert.Contains(">Fourth<", EngineFixture.Body(planned, new Bulletin("Fourth", "Four")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-219")]
    public async Task LibraryPropertiesInListItemsAreSlotsAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            Themed(Items),
            new Bulletins([new Bulletin("First", "One", Initials: "FO")]),
            new Bulletins([new Bulletin("Second", Initials: "SE"), new Bulletin("Third", "Three", Initials: "TH")]));

        Assert.Contains(">TH<", EngineFixture.Body(planned, new Bulletins([new Bulletin("Third", "Three", Initials: "TH")])), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-220")]
    public async Task LibraryDecoratorValuesAreLearnedAsync()
    {
        const string Body = """<m:Proportion Value="{Binding Shape}" Width="200"><Border Background="Red" /></m:Proportion>""";

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Bulletin("A", Shape: 2));

        Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Bulletin("A", Shape: 4)));
    }

    [Fact]
    [Trait("AC", "ENGINE-221")]
    public async Task BoundOpenFlagsOfFlyoutsArePlannedAsync()
    {
        const string Body = """<Button Content="Open" m:Pop.IsOpen="{Binding Open}"><Button.Flyout><Flyout><TextBlock Text="Inside" /></Flyout></Button.Flyout></Button>""";

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Bulletin("A"), new Bulletin("A", Open: true));

        Assert.DoesNotContain("data-aioros-open", EngineFixture.Body(planned, new Bulletin("A")), StringComparison.Ordinal);
        Assert.Contains("data-aioros-open", EngineFixture.Body(planned, new Bulletin("A", Open: true)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-285")]
    public async Task BoundFlagsOfLibraryButtonsArePlannedAsync()
    {
        var body = Themed("""<m:Tab Content="Inbox" IsActive="{Binding Open}" />""");

        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Bulletin("A"), new Bulletin("A", Open: true));

        Assert.NotEqual(EngineFixture.Body(planned, new Bulletin("A")), EngineFixture.Body(planned, new Bulletin("A", Open: true)));
    }

    [Fact]
    [Trait("AC", "ENGINE-286")]
    public async Task FlyoutsOpenFromTheStartArePlannedAsync()
    {
        const string Body = """<Button Content="Open" m:Pop.IsOpen="{Binding Open}"><Button.Flyout><Flyout><StackPanel><TextBlock Text="Inside" /></StackPanel></Flyout></Button.Flyout></Button>""";

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Bulletin("A", Open: true), new Bulletin("A"));

        Assert.Contains("data-aioros-open", EngineFixture.Body(planned, new Bulletin("A", Open: true)), StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-open", EngineFixture.Body(planned, new Bulletin("A")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-288")]
    public async Task ControlsHiddenInTheFirstItemArePlannedAsTheyShowAsync()
    {
        const string Body = """<ItemsControl ItemsSource="{Binding Items}"><ItemsControl.ItemTemplate><DataTemplate x:DataType="m:Bulletin"><Panel><TextBlock Text="{Binding Title}" IsVisible="{Binding Open}" /><ComboBox Width="152" PlaceholderText="Assign" IsVisible="{Binding !Open}"><x:String>Eddie</x:String><x:String>Jamik</x:String></ComboBox></Panel></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>""";

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Bulletins([new Bulletin("A", Open: true), new Bulletin("C")]), new Bulletins([new Bulletin("C"), new Bulletin("D")]));

        Assert.Equal(2, EngineFixture.Body(planned, new Bulletins([new Bulletin("C"), new Bulletin("D")])).Split("<select", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    [Trait("AC", "ENGINE-292")]
    public async Task PathsStrokedWithTheForegroundOfALearnedLibraryControlArePlannedAsync()
    {
        var body = Themed("""<m:Phase Content="Thinking" Status="{Binding Phase}"><m:Phase.Icon><Path Width="10" Height="10" StrokeThickness="2" Stroke="{Binding $parent[m:Phase].Foreground}" Data="M0,0 L10,10" /></m:Phase.Icon></m:Phase>""");

        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Bulletin("A"), new Bulletin("A", Phase: PhaseStatus.Running));

        Assert.Contains("#0a0a0a", EngineFixture.Body(planned, new Bulletin("A")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#737373", EngineFixture.Body(planned, new Bulletin("A", Phase: PhaseStatus.Running)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("AC", "ENGINE-293")]
    public async Task PathsStrokedWithTheForegroundOfAButtonEnabledByTheModelArePlannedAsync()
    {
        const string Body = """<Button IsEnabled="{Binding Idle}" Command="{Binding ReplayCommand}"><StackPanel Orientation="Horizontal" Spacing="4"><Path Width="10" Height="10" StrokeThickness="2" Stroke="{Binding $parent[Button].Foreground}" Data="M0,0 L10,10" /><TextBlock Text="Replay" /></StackPanel></Button>""";

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Turn { Idle = true }, new Turn());

        Assert.NotEqual(EngineFixture.Body(planned, new Turn { Idle = true }), EngineFixture.Body(planned, new Turn()));
    }

    [Fact]
    [Trait("AC", "ENGINE-294")]
    public async Task PathsStrokedWithTheForegroundOfALineShownByTheModelArePlannedAsync()
    {
        var body = Themed("""<m:Phase IsVisible="{Binding Asked}" Content="Thinking" Status="{Binding Phase}"><m:Phase.Icon><Path Width="10" Height="10" StrokeThickness="2" Stroke="{Binding $parent[m:Phase].Foreground}" Data="M0,0 L10,10" /></m:Phase.Icon></m:Phase>""");

        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Turn(), new Turn { Asked = true }, new Turn { Asked = true, Phase = PhaseStatus.Running });

        Assert.Contains("#737373", EngineFixture.Body(planned, new Turn { Asked = true, Phase = PhaseStatus.Running }), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("AC", "ENGINE-295")]
    public async Task PathsStrokedWithTheForegroundOfALineWithBoundTextArePlannedAsync()
    {
        var body = Themed("""<Expander><Expander.Header><m:Phase Content="{Binding Title}" Status="{Binding Phase}"><m:Phase.Icon><Path Width="10" Height="10" StrokeThickness="2" Stroke="{Binding $parent[m:Phase].Foreground}" Data="M0,0 L10,10" /></m:Phase.Icon></m:Phase></Expander.Header><TextBlock Text="Steps" /></Expander>""");

        var planned = await fixture.PlansLikeAvaloniaAsync(body, new Bulletin("Thinking"), new Bulletin("Thought", Phase: PhaseStatus.Running));

        Assert.Contains(">Thought<", EngineFixture.Body(planned, new Bulletin("Thought", Phase: PhaseStatus.Running)), StringComparison.Ordinal);
    }
}
