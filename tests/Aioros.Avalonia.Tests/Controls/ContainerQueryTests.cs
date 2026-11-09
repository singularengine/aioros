using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ContainerQueryTests(EngineFixture fixture)
{
    private Task<List<string>> DifferencesAsync(string body, params int[] widths) => DifferencesAsync(body, static text => text.X, widths);

    private async Task<List<string>> DifferencesAsync(string body, Func<LaidOutText, double> place, int[] widths)
    {
        var page = await fixture.CompileAsync(body);
        var wrong = new List<string>();
        foreach (var width in widths)
        {
            var desktop = Places((await fixture.CaptureInAsync(body, new CaptureOptions("light", width, 400))).Texts, place);
            var web = Places(await BrowserLayout.TextsAsync(page, width, 400), place);
            wrong.AddRange(desktop.SequenceEqual(web) ? [] : [$"{width}: desktop {string.Join(", ", desktop)}, web {string.Join(", ", web)}"]);
        }

        return wrong;
    }

    private static ImmutableArray<string> Places(ImmutableArray<LaidOutText> texts, Func<LaidOutText, double> place) =>
        [.. texts.OrderBy(text => text.Y).ThenBy(text => text.X).Select(text => $"{text.Text}@{Math.Round(place(text))}")];

    [Fact]
    [Trait("AC", "LAYOUT-81")]
    public async Task AContainerQueryIsWrittenAsAContainerRuleAsync()
    {
        const string Body = """
            <Border Container.Name="card" Container.Sizing="Width" Padding="16">
              <Border.Styles>
                <ContainerQuery Name="card" Query="max-width:500">
                  <Style Selector="TextBlock.wide"><Setter Property="IsVisible" Value="False" /></Style>
                  <Style Selector="TextBlock.always"><Setter Property="Margin" Value="24,0,0,0" /></Style>
                </ContainerQuery>
              </Border.Styles>
              <StackPanel>
                <TextBlock Classes="wide" Text="Wide only" />
                <TextBlock Classes="always" Text="Always" />
              </StackPanel>
            </Border>
            """;
        var page = await fixture.CompileAsync(Body);

        var wrong = await DifferencesAsync(Body, 400, 490, 510, 800);

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        Assert.Contains("container-type:inline-size;container-name:a-card;", page.Css, StringComparison.Ordinal);
        Assert.Contains("@container a-card (max-width:468px)", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-82")]
    public async Task CombinedQueriesAndQueriesHoldingWhenWrittenFollowTheContainerAsync()
    {
        const string Body = """
            <Border Container.Name="band" Container.Sizing="Width">
              <Border.Styles>
                <Style Selector="TextBlock.banded"><Setter Property="IsVisible" Value="False" /></Style>
                <ContainerQuery Name="band" Query="min-width:600 and max-width:900, max-width:300">
                  <Style Selector="TextBlock.banded"><Setter Property="IsVisible" Value="True" /></Style>
                </ContainerQuery>
                <ContainerQuery Name="band" Query="min-width:1000">
                  <Style Selector="TextBlock.narrow"><Setter Property="IsVisible" Value="False" /></Style>
                </ContainerQuery>
              </Border.Styles>
              <StackPanel>
                <TextBlock Text="First" />
                <TextBlock Classes="banded" Text="Banded" />
                <TextBlock Classes="narrow" Text="Narrow" />
              </StackPanel>
            </Border>
            """;
        var page = await fixture.CompileAsync(Body);

        var wrong = await DifferencesAsync(Body, 250, 450, 700, 950, 1100);

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        Assert.Contains("@container a-band not (min-width:1000px)", page.Css, StringComparison.Ordinal);
        Assert.Contains("@container a-band (((min-width:600px) and (max-width:900px)) or (max-width:300px))", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "LAYOUT-83")]
    public async Task AnAlignedContainerTakesItsSlotAsync()
    {
        const string Body = """
            <Border Container.Sizing="Width" HorizontalAlignment="Left" MaxWidth="500">
              <Border.Styles>
                <ContainerQuery Query="max-width:450">
                  <Style Selector="TextBlock.end"><Setter Property="Margin" Value="0,0,40,0" /></Style>
                </ContainerQuery>
              </Border.Styles>
              <TextBlock Classes="end" TextAlignment="Right" Text="End" />
            </Border>
            """;

        var wrong = await DifferencesAsync(Body, static text => text.X + text.Width, [400, 800]);

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    [Trait("AC", "LAYOUT-84")]
    public async Task ANestedContainerFollowsTheQueriesAroundItAsync()
    {
        const string Body = """
            <Border Container.Name="block" Container.Sizing="Width">
              <Border.Styles>
                <Style Selector="Border.inner"><Setter Property="Margin" Value="40" /></Style>
                <ContainerQuery Name="block" Query="max-width:450">
                  <Style Selector="Border.inner"><Setter Property="Margin" Value="24" /></Style>
                </ContainerQuery>
                <ContainerQuery Name="inner" Query="min-width:300">
                  <Style Selector="TextBlock.moved"><Setter Property="Margin" Value="16,0,0,0" /></Style>
                  <Style Selector="Border.inner"><Setter Property="Padding" Value="30" /></Style>
                </ContainerQuery>
              </Border.Styles>
              <Border Classes="inner" Container.Name="inner" Container.Sizing="Width">
                <StackPanel>
                  <TextBlock Text="Still" />
                  <TextBlock Classes="moved" Text="Moved" />
                </StackPanel>
              </Border>
            </Border>
            """;

        var wrong = new List<string>();
        var page = await fixture.CompileAsync(Body);
        foreach (var width in (int[])[300, 400, 800])
        {
            var desktop = Corners((await fixture.CaptureInAsync(Body, new CaptureOptions("light", width, 400))).Texts);
            var web = Corners(await BrowserLayout.TextsAsync(page, width, 400));
            wrong.AddRange(desktop.SequenceEqual(web) ? [] : [$"{width}: desktop {string.Join(", ", desktop)}, web {string.Join(", ", web)}"]);
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    private static ImmutableArray<string> Corners(ImmutableArray<LaidOutText> texts) =>
        [.. texts.OrderBy(text => text.Y).ThenBy(text => text.X).Select(text => $"{text.Text}@{Math.Round(text.X)},{Math.Round(text.Y)}")];

    [Fact]
    [Trait("AC", "NAVIGATION-109")]
    public async Task AContainerQueryHidesATabsButtonAsync()
    {
        const string Body = """
            <Border Container.Name="strip" Container.Sizing="Width">
              <Border.Styles>
                <ContainerQuery Name="strip" Query="max-width:520">
                  <Style Selector="TabItem.wide"><Setter Property="IsVisible" Value="False" /></Style>
                </ContainerQuery>
              </Border.Styles>
              <TabControl>
                <TabItem Header="One"><TextBlock Text="First" /></TabItem>
                <TabItem Classes="wide" Header="Two"><TextBlock Text="Second" /></TabItem>
                <TabItem Header="Three"><TextBlock Text="Third" /></TabItem>
              </TabControl>
            </Border>
            """;
        var page = await fixture.CompileAsync(Body);

        var wrong = await DifferencesAsync(Body, static _ => 0, [400, 700]);
        var narrow = await BrowserLayout.TextsAsync(page, 400, 400);
        var wide = await BrowserLayout.TextsAsync(page, 700, 400);

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        Assert.DoesNotContain(narrow, static text => text.Text == "Two");
        Assert.Contains(wide, static text => text.Text == "Two");
    }

    [Fact]
    [Trait("AC", "ENGINE-417")]
    public async Task AListTracedInAContainerQueryCountsItsItemsOnceAsync()
    {
        const string Body = """
            <Border Container.Name="card" Container.Sizing="Width">
              <Border.Styles>
                <ContainerQuery Name="card" Query="max-width:500">
                  <Style Selector="TextBlock.wide"><Setter Property="IsVisible" Value="False" /></Style>
                </ContainerQuery>
              </Border.Styles>
              <StackPanel>
                <TextBlock Classes="wide" Text="Wide only" />
                <ItemsControl ItemsSource="{Binding Items}">
                  <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:Poster"><TextBox Text="{Binding Title}" /></DataTemplate></ItemsControl.ItemTemplate>
                </ItemsControl>
              </StackPanel>
            </Border>
            """;

        var page = await fixture.CompileAsync(Body, new Aioros.Avalonia.Tests.Models.Posters(new("/a.png", "Alpha"), new("/b.png", "Beta")));

        Assert.True(page.Reusable, page.Failure);
    }

    [Fact]
    [Trait("AC", "ENGINE-418")]
    public async Task ABoundListInAnUnboundListCountsInItsRegionAsync()
    {
        const string Body = """
            <ItemsControl>
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:Poster"><TextBox Text="{Binding Title}" /></DataTemplate></ItemsControl.ItemTemplate>
              </ItemsControl>
            </ItemsControl>
            """;

        var page = await fixture.CompileAsync(Body, new Aioros.Avalonia.Tests.Models.Posters(new("/a.png", "Alpha"), new("/b.png", "Beta")));

        Assert.True(page.Reusable, page.Failure);
    }
}
