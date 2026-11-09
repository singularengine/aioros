using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class LibraryListTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-240")]
    public async Task ListsALibraryBuildsFromItsTextArePlannedAsync()
    {
        const string Body = """
            <StackPanel x:DataType="m:OtpModel">
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/OtpTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <m:InputOtp Length="6" GroupSize="3" />
              <m:InputOtp Length="4" IsEnabled="False" />
              <m:InputOtp Length="6" Text="{Binding Code}" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new OtpModel(string.Empty));

        Assert.Equal(16, EngineFixture.Body(page, new OtpModel(string.Empty)).Split("role=\"listitem\"").Length - 1);
    }

    [Fact]
    [Trait("AC", "TEXT-53")]
    public async Task CodeSlotsAreMarkedForTheScriptWithTheirActiveLookAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/OtpSlotsTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <StackPanel.Styles>
                <Style Selector="m|InputOtp Border.otp-box">
                  <Setter Property="BorderBrush" Value="#e5e5e5" />
                </Style>
                <Style Selector="m|InputOtp Border.otp-box.active">
                  <Setter Property="BorderBrush" Value="#a1a1a1" />
                  <Setter Property="BoxShadow" Value="0 0 0 3 #80a1a1a1" />
                </Style>
              </StackPanel.Styles>
              <m:InputOtp Theme="{StaticResource SlotsOtp}" Length="6" Text="{Binding Code}" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new OtpModel(string.Empty));
        var html = EngineFixture.Body(page, new OtpModel(string.Empty));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-aioros-slots[ >=]"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<input[^>]* data-aioros-slots-input"));
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Count(html, "data-aioros-slot-box"));
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Count(html, "data-aioros-slot-text"));
        var box = System.Text.RegularExpressions.Regex.Match(html, "data-aioros-slot-box[^>]* class=\"([^\"]+)\"").Groups[1].Value.Split(' ');
        var rule = page.Css.Split('\n').FirstOrDefault(line => line.Contains("[data-aioros-active]", StringComparison.Ordinal) && box.Any(name => line.Contains("." + name, StringComparison.Ordinal))) ?? string.Empty;
        Assert.Contains("border-color:#a1a1a1", rule, StringComparison.Ordinal);
        Assert.Contains("0px 0px 0px 3px", rule, StringComparison.Ordinal);
    }
}
