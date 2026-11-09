using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class RowLayoutTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "LAYOUT-71")]
    public async Task AHorizontalStacksChildrenKeepTheirWidthAsync()
    {
        const string Body = """
            <StackPanel Orientation="Horizontal" Spacing="4">
              <Border Padding="8,4"><StackPanel Orientation="Horizontal" Spacing="4"><TextBlock Text="&lt;" /><TextBlock Text="Previous" /></StackPanel></Border>
              <Border Padding="8,4"><TextBlock Text="One" /></Border>
              <Border Padding="8,4"><TextBlock Text="Two" /></Border>
              <Border Padding="8,4"><TextBlock Text="Three" /></Border>
              <Border Padding="8,4"><TextBlock Text="Four" /></Border>
              <Border Padding="8,4"><TextBlock Text="Five" /></Border>
              <Border Padding="8,4"><StackPanel Orientation="Horizontal" Spacing="4"><TextBlock Text="Next" /><TextBlock Text="&gt;" /></StackPanel></Border>
            </StackPanel>
            """;
        var desktop = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 200))).Texts;
        var web = await BrowserLayout.TextsAsync(await fixture.CompileAsync(Body), 300, 200);

        Assert.Equal(Gaps(desktop), Gaps(web));
        Assert.Equal([.. Enumerable.Repeat(true, 8)], Gaps(web));
    }

    [Fact]
    [Trait("AC", "LAYOUT-72")]
    public async Task AHorizontalStackKeepsItsChildrenWidthsWithoutWrappersAsync()
    {
        var page = await fixture.CompileAsync("""
            <ItemsControl ItemsSource="{Binding Tags}">
              <ItemsControl.ItemsPanel><ItemsPanelTemplate><StackPanel Orientation="Horizontal" Spacing="4" /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
              <ItemsControl.ItemTemplate><DataTemplate x:DataType="sys:String"><Border Padding="4"><TextBlock Text="{Binding}" /></Border></DataTemplate></ItemsControl.ItemTemplate>
            </ItemsControl>
            """, new Models.Tagged(["One", "Two"]));
        var html = EngineFixture.Body(page, new Models.Tagged(["One", "Two"]));

        Assert.Matches("^<div class=\"c hs k[0-9a-f]+\" role=\"list\"><div role=\"listitem\" class=\"li\"><div class=\"c k[0-9a-f]+\"><div class=\"k[0-9a-f]+\">One</div></div></div>", html);
        Assert.Contains(".c.hs>*,.c.hs>:is(.li,[style*=\"display:contents\"])>*,.c.hs>:is(.li,[style*=\"display:contents\"])>:is(.li,[style*=\"display:contents\"])>*{flex-shrink:0}", page.Css, StringComparison.Ordinal);
    }

    private static ImmutableArray<bool> Gaps(ImmutableArray<LaidOutText> texts)
    {
        var row = texts.OrderBy(text => text.X).ToList();
        return [.. row.Zip(row.Skip(1), (left, right) => right.X >= left.X + left.Width + 3)];
    }
}
