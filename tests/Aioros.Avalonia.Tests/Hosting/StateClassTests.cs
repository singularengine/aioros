using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class StateClassTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-211")]
    public async Task BoundClassesKeepOneStateClassInItemTemplatesAsync()
    {
        var model = new Chips(new Toggled("One", true), new Toggled("Two", false));
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.Styles>
                <Style Selector="Border.chip:pointerover"><Setter Property="Background" Value="#eeeeee" /></Style>
                <Style Selector="Border.chip.on"><Setter Property="Background" Value="#111111" /></Style>
                <Style Selector="Border.chip.on:pointerover"><Setter Property="Background" Value="#333333" /></Style>
              </ItemsControl.Styles>
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="m:Toggled">
                  <Border Classes="chip" Classes.on="{Binding On}" a:Web.Href="/chips"><TextBlock Text="{Binding Name}" /></Border>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """,
            model,
            new Chips(new Toggled("Three", false), new Toggled("Four", true)));

        var html = EngineFixture.Body(page, model);
        Assert.DoesNotMatch("class=\"[^\"]*\\b(h[0-9a-f]{8})\\b[^\"]*\\b\\1\\b", html);
        Assert.Equal(2, Regex.Count(html, "\\bh[0-9a-f]{8}\\b"));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-90")]
    public async Task ATipShownByOneVariantDescribesOnlyThatVariantAsync()
    {
        var model = new Chips(new Toggled("One", true), new Toggled("Two", false));
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.Styles>
                <Style Selector="Button.chip.on"><Setter Property="ToolTip.Tip" Value="Pinned" /></Style>
              </ItemsControl.Styles>
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="m:Toggled">
                  <Button Classes="chip" Classes.on="{Binding On}" Content="{Binding Name}" />
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """,
            model,
            new Chips(new Toggled("Three", false), new Toggled("Four", true)));

        var html = EngineFixture.Body(page, model);
        Assert.Equal(1, Regex.Count(html, "aria-describedby="));
    }
}
