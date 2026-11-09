using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class LearnedPlanTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-162")]
    public async Task GeometriesFromConvertersAreLearnedPerValueAsync()
    {
        const string Xaml = """
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="m:Item">
                  <StackPanel Orientation="Horizontal" Spacing="4">
                    <PathIcon Width="16" Height="16" Data="{Binding Name, Converter={x:Static m:NameIcons.Instance}}" />
                    <TextBlock Text="{Binding Name}" />
                  </StackPanel>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """;
        var planned = await fixture.PlansLikeAvaloniaAsync(
            Xaml,
            new Catalog(new Item("a"), new Item("bb"), new Item("ccc")),
            new Catalog(new Item("ccc"), new Item("a"), new Item("a")),
            new Catalog());
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        var miss = Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Catalog(new Item("dddd"))));
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(saved, new Catalog(new Item("dddd"))));
        Assert.Contains("dddd", miss.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-308")]
    public async Task PlainValuesBoundThroughConvertersArePlannedAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Resources><m:SizeIs x:Key="Is" /><m:CornerConverter x:Key="Corners" /></StackPanel.Resources>
              <CheckBox Content="Large print" IsChecked="{Binding Print, Converter={StaticResource Is}, ConverterParameter=Large}" />
              <Border Width="20" Height="20" Background="Red" CornerRadius="{Binding Agree, Converter={StaticResource Corners}}" />
            </StackPanel>
            """;
        var planned = await fixture.PlansLikeAvaloniaAsync(Xaml, new ParityModel(), new ParityModel { Print = Sized.Large, Agree = true }, new ParityModel { Print = Sized.Medium });
        var model = new ParityModel();

        var posted = await PlanForms.ApplyAsync(planned.Plan, [model], System.Collections.Immutable.ImmutableDictionary.CreateRange([KeyValuePair.Create("_chg", "0"), KeyValuePair.Create("f0", "on")]), new Plans.AdapterSlotResolver());

        Assert.Contains("border-radius:8px", EngineFixture.Body(planned, new ParityModel { Agree = true }), StringComparison.Ordinal);
        Assert.Equal(PostStatus.Rendered, posted);
        Assert.Equal(Sized.Large, model.Print);
    }

    [Fact]
    [Trait("AC", "ENGINE-164")]
    public async Task ThemeVariantScopesAreWrittenForEachVariantAsync()
    {
        const string Xaml = """
            <ThemeVariantScope RequestedThemeVariant="{Binding Variant}">
              <Border Background="{DynamicResource SystemControlBackgroundAltHighBrush}" Padding="4">
                <TextBlock Text="themed" Foreground="{DynamicResource SystemControlForegroundBaseHighBrush}" />
              </Border>
            </ThemeVariantScope>
            """;
        var planned = await fixture.PlansLikeAvaloniaAsync(
            Xaml,
            new Scoped(null),
            new Scoped(global::Avalonia.Styling.ThemeVariant.Dark),
            new Scoped(global::Avalonia.Styling.ThemeVariant.Light),
            new Scoped(global::Avalonia.Styling.ThemeVariant.Default));

        Assert.NotEqual(EngineFixture.Body(planned, new Scoped(global::Avalonia.Styling.ThemeVariant.Dark)), EngineFixture.Body(planned, new Scoped(global::Avalonia.Styling.ThemeVariant.Light)));
        Assert.Throws<PlanMissException>(() => EngineFixture.Body(planned, new Scoped(new global::Avalonia.Styling.ThemeVariant("Sepia", global::Avalonia.Styling.ThemeVariant.Light))));
    }
}
