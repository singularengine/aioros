using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class PlanReachTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-160")]
    public async Task HiddenControlsAreNotCheckedAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            """<StackPanel><TextBlock Text="shown" /><TextBlock Text="hidden" MaxWidth="{Binding Size}" IsVisible="False" /></StackPanel>""",
            new Dated(10, DateTime.UnixEpoch, null), new Dated(20, DateTime.UnixEpoch, null));
        var shown = await fixture.CompileAsync("""<TextBlock Text="hidden" MaxWidth="{Binding Size}" />""", new Dated(10, DateTime.UnixEpoch, null));

        var classed = await fixture.PlansLikeAvaloniaAsync(
            """<StackPanel><StackPanel.Styles><Style Selector="TextBlock.gone"><Setter Property="IsVisible" Value="False" /></Style></StackPanel.Styles><TextBlock Text="shown later" Classes.gone="{Binding !Show}" /></StackPanel>""",
            new Secret(false), new Secret(true));

        Assert.DoesNotContain("hidden", EngineFixture.Body(planned, new Dated(10, DateTime.UnixEpoch, null)), StringComparison.Ordinal);
        Assert.Contains("shown later", EngineFixture.Body(classed, new Secret(true)), StringComparison.Ordinal);
        Assert.False(shown.Reusable);
        Assert.Contains("MaxWidth", shown.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-161")]
    public async Task PresentersShowTheirControlsBoundValuesAsync()
    {
        const string Xaml = """
            <m:Captioned Header="{Binding Who}" Content="body">
              <m:Captioned.HeaderTemplate>
                <DataTemplate x:DataType="m:Person"><TextBlock Text="{Binding Name}" FontWeight="Bold" /></DataTemplate>
              </m:Captioned.HeaderTemplate>
            </m:Captioned>
            """;
        var planned = await fixture.PlansLikeAvaloniaAsync(Xaml, new Feeling(Mood.Calm, true, new Person("Ann")), new Feeling(Mood.Busy, false, new Person("Bob")), new Feeling(Mood.Away, true));
        var unknown = await fixture.CompileAsync(Xaml, new Feeling(Mood.Calm, true));

        Assert.Contains(">Bob<", EngineFixture.Body(planned, new Feeling(Mood.Busy, false, new Person("Bob"))), StringComparison.Ordinal);
        Assert.False(unknown.Reusable);
        Assert.Contains("null", unknown.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-81")]
    public async Task NullListsLearnFromTheirDeclaredItemsAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            """
            <ItemsControl ItemsSource="{Binding Tags}">
              <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
              <ItemsControl.ItemTemplate><DataTemplate><Label Content="{Binding .}" /></DataTemplate></ItemsControl.ItemTemplate>
            </ItemsControl>
            """,
            new Tagged(null), new Tagged(["one", "two"]), new Tagged([]));

        var wrapped = await fixture.PlansLikeAvaloniaAsync(
            """
            <Border DataContext="{Binding Inner}">
              <ItemsControl ItemsSource="{Binding Tags}">
                <ItemsControl.ItemTemplate><DataTemplate><Label Content="{Binding .}" /></DataTemplate></ItemsControl.ItemTemplate>
              </ItemsControl>
            </Border>
            """,
            new Wrapped(null), new Wrapped(new Tagged(["three"])), new Wrapped(new Tagged(null)));

        Assert.Contains(">two</label>", EngineFixture.Body(planned, new Tagged(["one", "two"])), StringComparison.Ordinal);
        Assert.Contains(">three</label>", EngineFixture.Body(wrapped, new Wrapped(new Tagged(["three"]))), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-163")]
    public async Task MultiBindingsOfConstantTemplatedPropertiesAreWrittenAsRenderedAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="HeaderedContentControl.badge">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Background="Red">
                        <Border.IsVisible>
                          <MultiBinding Converter="{x:Static BoolConverters.Or}">
                            <Binding Path="Header" RelativeSource="{RelativeSource TemplatedParent}" Converter="{x:Static ObjectConverters.IsNotNull}" />
                            <Binding Path="Tag" RelativeSource="{RelativeSource TemplatedParent}" />
                          </MultiBinding>
                        </Border.IsVisible>
                        <TextBlock Text="badge" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </Style>
              </StackPanel.Styles>
              <m:Captioned Classes="badge" Header="5" />
              <m:Captioned Classes="badge" Tag="{x:False}" />
            </StackPanel>
            """);

        Assert.True(page.Reusable, page.Failure);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(EngineFixture.Body(page), ">badge<"));
    }

    [Fact]
    [Trait("AC", "ENGINE-165")]
    public async Task ItemsThatAreConstantsAreWrittenAsRenderedAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            """
            <StackPanel>
              <ItemsControl ItemsSource="{x:Static m:Swatches.Names}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate><Border ToolTip.Tip="{Binding Converter={x:Static m:TextConverters.Whisper}}"><TextBlock Text="{Binding}" /></Border></DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <ItemsControl ItemsSource="{x:Static m:Swatches.Sizes}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate><Border ToolTip.Tip="{Binding}"><TextBlock Text="size" /></Border></DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <ItemsControl ItemsSource="{Binding Tags}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate><Border ToolTip.Tip="{Binding Converter={x:Static m:TextConverters.Whisper}}"><TextBlock Text="{Binding}" /></Border></DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """,
            new Tagged(["Green"]), new Tagged(["Gray", "Pink"]));

        Assert.Contains("red...", EngineFixture.Body(planned, new Tagged(["Green"])), StringComparison.Ordinal);
        Assert.Contains("pink...", EngineFixture.Body(planned, new Tagged(["Gray", "Pink"])), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-178")]
    public async Task TemplatesReadingTheirOwnConstantsAreWrittenAsRenderedAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync("""<ColorView Color="#0D6EFD" />""", new Gauged(1));

        Assert.Contains("<input", EngineFixture.Body(planned, new Gauged(1)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-179")]
    public async Task StyleBindingsToConstantElementsAreConstantsAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="ColorView"><Setter Property="IsAlphaVisible" Value="{Binding #alpha.IsChecked}" /></Style>
              </StackPanel.Styles>
              <CheckBox Name="alpha" IsChecked="False" />
              <ColorView Color="#0D6EFD" />
            </StackPanel>
            """;
        var planned = await fixture.PlansLikeAvaloniaAsync(Body, new Secret(false));
        var bound = await fixture.CompileAsync(Body.Replace("IsChecked=\"False\"", "IsChecked=\"{Binding Show}\"", StringComparison.Ordinal), new Secret(false));

        Assert.True(planned.Reusable, planned.Failure);
        Assert.False(bound.Reusable);
    }
}
