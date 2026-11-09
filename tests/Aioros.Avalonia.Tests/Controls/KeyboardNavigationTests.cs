using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class KeyboardNavigationTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "FOCUS-10")]
    public async Task KeyboardXyNavigationIsWrittenAsRovingFocusAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <StackPanel a:Web.Id="row" Orientation="Horizontal" XYFocus.NavigationModes="Keyboard"><Button Content="A" /><StackPanel a:Web.Id="inner" Orientation="Horizontal" Spacing="2"><Button Content="B" /><Button Content="B2" /></StackPanel></StackPanel>
              <StackPanel a:Web.Id="column" XYFocus.NavigationModes="Enabled"><Button Content="C" /><Button Content="D" /></StackPanel>
              <WrapPanel a:Web.Id="wrap" XYFocus.NavigationModes="Keyboard" KeyboardNavigation.TabNavigation="Once"><Button Content="E" /><Button Content="F" /></WrapPanel>
              <StackPanel a:Web.Id="plain"><Button Content="G" /></StackPanel>
            </StackPanel>
            """);

        string Open(string id) => System.Text.RegularExpressions.Regex.Match(html, "<div[^>]* id=\"" + id + "\"[^>]*>").Value;
        Assert.Contains("data-aioros-roving=\"horizontal\"", Open("row"), StringComparison.Ordinal);
        Assert.Contains("data-aioros-roving-tabs=\"keep\"", Open("row"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-roving", Open("inner"), StringComparison.Ordinal);
        Assert.Contains("data-aioros-roving=\"vertical\"", Open("column"), StringComparison.Ordinal);
        Assert.Contains("data-aioros-roving-tabs=\"keep\"", Open("column"), StringComparison.Ordinal);
        Assert.Contains("data-aioros-roving=\"both\"", Open("wrap"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-aioros-roving-tabs", Open("wrap"), StringComparison.Ordinal);
        Assert.All(["row", "column", "wrap"], id => Assert.Contains("data-aioros-roving-ends=\"off\"", Open(id), StringComparison.Ordinal));
        Assert.DoesNotContain("data-aioros-roving", Open("plain"), StringComparison.Ordinal);
    }
}
