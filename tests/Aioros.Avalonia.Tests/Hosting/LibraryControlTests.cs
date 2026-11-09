using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class LibraryControlTests(EngineFixture fixture)
{
    private static string Themed(string body) => $"""
        <StackPanel>
          <StackPanel.Resources>
            <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/GaugeTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
          </StackPanel.Resources>
          {body}
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-175")]
    public async Task OwnValuesShownByCodeAreLearnedAsync()
    {
        var xaml = Themed("""<m:Gauge Level="{Binding Level}" Marks="{Binding Marks}" Tag="{Binding Title}" Title="{Binding Title}" />""");
        var planned = await fixture.PlansLikeAvaloniaAsync(xaml, new Gauged(3, Marks: ["a", "b"]), new Gauged(3, Marks: ["a", "b"]));
        var tagged = await fixture.PlansLikeAvaloniaAsync(xaml, new Gauged(3, "x"), new Gauged(3, "x"));
        var unkeyed = await fixture.CompileAsync(Themed("""<m:Gauge Level="2" Picked="{Binding Picked}" />"""), new Gauged(2, Picked: new Person("Ann")));

        Assert.Contains(">###<", EngineFixture.Body(planned, new Gauged(3, Marks: ["a", "b"])), StringComparison.Ordinal);
        Assert.Contains("4", Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Gauged(4, Marks: ["a", "b"]))).Message, StringComparison.Ordinal);
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Gauged(3, Marks: ["a"])));
        Assert.Contains(">tagged<", EngineFixture.Body(tagged, new Gauged(3, "x")), StringComparison.Ordinal);
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(tagged, new Gauged(3)));
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(tagged, new Gauged(3, "y")));
        Assert.False(unkeyed.Reusable);
        Assert.Contains("Gauge.Picked", unkeyed.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-268")]
    public async Task ValuesALibraryWritesBackAreLearnedAsAFreshModelHoldsThemAsync()
    {
        var xaml = Themed("""<m:Defaulted Choice="{Binding Choice}" />""");
        var compiled = new Defaults();
        var planned = await fixture.CompileAsync(xaml, compiled);
        var avalonia = new Defaults();
        var rendered = EngineFixture.Body(await fixture.CompileAsync(xaml, avalonia), avalonia);

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal("first", compiled.Choice);
        Assert.Contains(">Chose first<", rendered, StringComparison.Ordinal);
        Assert.Equal(rendered, EngineFixture.Body(planned, new Defaults()));
        Assert.Equal(rendered, EngineFixture.Body(planned, new Defaults { Choice = "first" }));
        Assert.Contains("second", Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Defaults { Choice = "second" })).Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-177")]
    public async Task LibraryListsAreLearnedWithTheirItemsAsync()
    {
        var chosen = await fixture.PlansLikeAvaloniaAsync(
            """<m:Chooser ItemsSource="{Binding Marks}" Chosen="{Binding Title}" />""",
            new Gauged(1, "b", Marks: ["a", "b", "c"]), new Gauged(1, "b", Marks: ["a", "b", "c"]));
        var repeated = await fixture.PlansLikeAvaloniaAsync(
            """<m:Chooser ItemsSource="{Binding Marks}" />""",
            new Gauged(1, Marks: ["a", "b"]), new Gauged(1, Marks: ["x", "y", "z"]), new Gauged(1, Marks: []));

        Assert.Contains(">c<", EngineFixture.Body(chosen, new Gauged(1, "b", Marks: ["a", "b", "c"])), StringComparison.Ordinal);
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(chosen, new Gauged(1, "b", Marks: ["a", "b"])));
        Assert.Contains(">z<", EngineFixture.Body(repeated, new Gauged(1, Marks: ["x", "y", "z"])), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-176")]
    public async Task TemplateBoundValuesWithoutKeysAreSlotsAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            Themed("""<m:Gauge Level="2" IsTabStop="{Binding Busy}" People="{Binding People}" />"""),
            new Gauged(2, People: [new Person("Ann")]), new Gauged(2, Busy: true, People: [new Person("Bob"), new Person("Cy")]), new Gauged(2, People: []));

        Assert.Contains(">Cy<", EngineFixture.Body(planned, new Gauged(2, Busy: true, People: [new Person("Bob"), new Person("Cy")])), StringComparison.Ordinal);
        Assert.DoesNotContain(">tab<", EngineFixture.Body(planned, new Gauged(2, People: [new Person("Ann")])), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-180")]
    public async Task TemplateBoundBrushesInListsAreInlineStylesAsync()
    {
        const string Items = """
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="m:Tint"><m:Gauge Level="1" Title="{Binding Name}" Background="{Binding Paint}" Foreground="{Binding Ink}" /></DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """;
        var red = new Tint("Red", global::Avalonia.Media.Brushes.Red, global::Avalonia.Media.Brushes.White);
        var planned = await fixture.PlansLikeAvaloniaAsync(
            Themed(Items),
            new Tints([red]), new Tints([new Tint("Blue", global::Avalonia.Media.Brushes.Blue, global::Avalonia.Media.Brushes.Yellow), red]));

        var inherited = await fixture.CompileAsync(Themed(Items.Replace("Foreground=\"{Binding Ink}\"", "Foreground=\"{Binding Ink}\" Marks=\"{Binding Marks}\"", StringComparison.Ordinal)), new Tints([red with { Marks = ["a"] }]));

        Assert.Contains(">Blue<", EngineFixture.Body(planned, new Tints([new Tint("Blue", global::Avalonia.Media.Brushes.Blue, global::Avalonia.Media.Brushes.Yellow)])), StringComparison.Ordinal);
        Assert.False(inherited.Reusable);
        Assert.Contains("Gauge.Foreground", inherited.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-181")]
    public async Task KeyedIndexerBindingsAreRefusedAsync()
    {
        var text = await fixture.CompileAsync("""<m:IndexedText />""", new Keyed(new() { ["red"] = new Tint("Red", global::Avalonia.Media.Brushes.Red, global::Avalonia.Media.Brushes.White) }));

        Assert.False(text.Reusable);
        Assert.Contains("TextBlock.Text", text.Failure, StringComparison.Ordinal);
        Assert.Contains("indexer", text.Failure, StringComparison.Ordinal);
        Assert.Contains(">Red<", EngineFixture.Body(text), StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "ENGINE-212")]
    public async Task BoundLibraryFlagsArePlannedForEveryValueAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <StackPanel.Styles><Style Selector="m|Flagged:invalid TextBlock"><Setter Property="Foreground" Value="#e7000b" /></Style></StackPanel.Styles>
              <m:Flagged IsInvalid="{Binding Busy}"><TextBlock Text="Email" /></m:Flagged>
            </StackPanel>
            """, new Gauged(1), new Gauged(1, Busy: true));

        Assert.Contains("#e7000b", planned.Css, StringComparison.Ordinal);
        Assert.NotEqual(EngineFixture.Body(planned, new Gauged(1)), EngineFixture.Body(planned, new Gauged(1, Busy: true)));
    }

    [Fact]
    [Trait("AC", "ENGINE-217")]
    public async Task ItemStatesByPositionArePlannedAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="{x:Type m:CrumbItem}" TargetType="m:CrumbItem">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel Orientation="Horizontal" Spacing="4">
                        <TextBlock Text="/" IsVisible="{TemplateBinding IsFirst, Converter={x:Static BoolConverters.Not}}" />
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <StackPanel.Styles>
                <Style Selector="ItemsControl.steps ContentPresenter:nth-last-child(1) TextBlock"><Setter Property="FontWeight" Value="Bold" /></Style>
              </StackPanel.Styles>
              <m:Crumbs ItemsSource="{Binding Crumbs}">
                <m:Crumbs.ItemsPanel><ItemsPanelTemplate><StackPanel Orientation="Horizontal" Spacing="8" /></ItemsPanelTemplate></m:Crumbs.ItemsPanel>
                <m:Crumbs.ItemTemplate><DataTemplate x:DataType="m:Crumb"><TextBlock Text="{Binding Name}" /></DataTemplate></m:Crumbs.ItemTemplate>
              </m:Crumbs>
              <ItemsControl Classes="steps" ItemsSource="{Binding Crumbs}">
                <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:Crumb"><TextBlock Text="{Binding Name}" /></DataTemplate></ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """;
        Trail Of(int count) => new([.. Enumerable.Range(1, count).Select(index => new Crumb("c" + index, "/" + index))]);

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, Of(3), Of(1), Of(2), Of(3), Of(5));

        Assert.Empty(System.Text.RegularExpressions.Regex.Matches(EngineFixture.Body(planned, Of(1)), ">/<"));
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Count(EngineFixture.Body(planned, Of(5)), ">/<"));
    }
}
