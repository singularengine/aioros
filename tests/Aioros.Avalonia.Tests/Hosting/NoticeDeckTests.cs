using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed partial class NoticeDeckTests(EngineFixture fixture)
{
    private static string Page(bool expanded) => $$"""
        <StackPanel>
          <StackPanel.Resources>
            <ControlTheme x:Key="Deck" TargetType="WindowNotificationManager">
              <Setter Property="Margin" Value="24" />
              <Setter Property="Template">
                <ControlTemplate>
                  <Grid Name="PART_Items" Width="356" HorizontalAlignment="Right" VerticalAlignment="Bottom"
                        a:Web.Deck="14" a:Web.DeckVisible="3" a:Web.DeckScale="0.05" a:Web.DeckBehind="behind" a:Web.DeckExpanded="{{expanded}}" />
                </ControlTemplate>
              </Setter>
            </ControlTheme>
            <ControlTheme x:Key="Card" TargetType="NotificationCard">
              <Setter Property="Transitions">
                <Transitions>
                  <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.4" />
                </Transitions>
              </Setter>
              <Setter Property="Template">
                <ControlTemplate>
                  <Border Name="PART_Root" Background="#ffffff" Padding="16">
                    <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                  </Border>
                </ControlTemplate>
              </Setter>
              <Style Selector="^.behind /template/ ContentPresenter#PART_ContentPresenter">
                <Setter Property="Opacity" Value="0" />
              </Style>
            </ControlTheme>
          </StackPanel.Resources>
          <m:DeckCardsHost Deck="{StaticResource Deck}" CardTheme="{StaticResource Card}" />
          <Button Command="{Binding StackCommand}" Content="Stack" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "OVERLAY-16")]
    public async Task ADeckWritesItsStackAsync()
    {
        var collapsed = (await fixture.PostAsync(Page(false), new Toasting(), ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken)).Page!;
        var html = EngineFixture.Body(collapsed, new Toasting());
        var layer = Regex.Match(html, "<div popover=\"manual\" class=\"notice na nd\" data-aioros-deck=\"14 3 0.05\" data-aioros-anchor=\"bottom\"[^>]* style=\"bottom:24px;right:24px;width:356px;height:([0-9.]+)px;\">");
        var cards = Cards().Matches(html).ToList();
        var heights = cards.ConvertAll(card => double.Parse(card.Groups["h"].Value, CultureInfo.InvariantCulture));
        var expandedPage = (await fixture.PostAsync(Page(true), new Toasting(), ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken)).Page!;
        var expanded = EngineFixture.Body(expandedPage, new Toasting());
        var spread = Cards().Matches(expanded).ToList();
        var faded = Regex.Match(html, "class=\"[^\"]*(db[0-9a-f]+)[^\"]*\"[^>]*>(?:<[^>]+>)*The newest card").Groups[1].Value;

        Assert.True(layer.Success, html);
        Assert.Equal(4, cards.Count);
        Assert.Equal(heights[3], double.Parse(layer.Groups[1].Value, CultureInfo.InvariantCulture));
        Assert.True(heights[3] > heights[2], $"{heights[3]} {heights[2]}");
        Assert.Equal(
            [("-42px", "scale:0.85;height:" + Px(heights[3]) + ";opacity:0;z-index:1;", true), ("-28px", "scale:0.9;height:" + Px(heights[3]) + ";z-index:2;", true), ("-14px", "scale:0.95;height:" + Px(heights[3]) + ";z-index:3;", true), ("0px", "z-index:4;", false)],
            cards.ConvertAll(card => (card.Groups["y"].Value, card.Groups["rest"].Value, card.Groups["behind"].Success)));
        var moving = cards[0].Groups["moving"].Value;
        Assert.All(cards, card => Assert.Equal(moving, card.Groups["moving"].Value));
        Assert.Contains($".notice.nd>.nt.{moving}{{transition:translate 400ms linear,scale 400ms linear,opacity 400ms linear,height 400ms linear}}", collapsed.Css, StringComparison.Ordinal);
        Assert.Contains($".notice.nd>.nt:not([data-behind]) .{faded}.{faded}{{opacity:1}}.notice.nd>.nt[data-behind] .{faded}.{faded}{{opacity:0}}", collapsed.Css, StringComparison.Ordinal);
        Assert.Contains("data-aioros-expanded", expanded, StringComparison.Ordinal);
        Assert.Equal(
            ["-" + Px(heights[3] + heights[2] + heights[1] + 42).TrimEnd('p', 'x') + "px", "-" + Px(heights[3] + heights[2] + 28), "-" + Px(heights[3] + 14), "0px"],
            spread.ConvertAll(card => card.Groups["y"].Value));
        Assert.All(spread, card => Assert.False(card.Groups["behind"].Success));
        Assert.DoesNotContain("scale:", string.Concat(spread.Select(card => card.Groups["rest"].Value)), StringComparison.Ordinal);
        Assert.Matches($"height:{Regex.Escape(Px(heights[3] + heights[2] + heights[1] + 28))};\">", expanded);
    }

    private static string Px(double value) => (Math.Round(value, 3) + 0.0).ToString(CultureInfo.InvariantCulture) + "px";

    [GeneratedRegex("<div class=\"nt (?<moving>dm[0-9a-f]+)\" role=\"status\" data-h=\"(?<h>[0-9.]+)\"(?<behind> data-behind)? style=\"--expire:5000ms;translate:0 (?<y>-?[0-9.]+px);(?<rest>[^\"]*)\">")]
    private static partial Regex Cards();
}
