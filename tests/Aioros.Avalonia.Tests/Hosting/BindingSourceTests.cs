using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class BindingSourceTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-60")]
    public async Task StaticSourcesAreWrittenAsRenderedAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Resources><m:Motto x:Key="motto" Words="Resource words" /></StackPanel.Resources>
              <StackPanel.Styles><Style Selector="TextBlock.loud"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles>
              <TextBlock Text="{Binding Text}" />
              <TextBlock Text="{Binding Source={x:Static m:Labels.Current}, Path=Title}" />
              <TextBlock Text="{Binding Path=Words, Source={StaticResource motto}}" />
              <TextBlock Text="{m:T greeting}" />
              <TextBlock Text="hidden" IsVisible="{Binding !Flag, Source={x:Static m:Labels.Current}}" />
              <TextBlock Text="classed" Classes.loud="{Binding Flag, Source={x:Static m:Labels.Current}}" />
              <Border Height="2" Tag="{Binding Title, Source={x:Static m:Labels.Current}, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />
              <TextBlock>
                <TextBlock.Text>
                  <MultiBinding StringFormat="{}{0} / {1}">
                    <Binding Source="{x:Static m:Labels.Current}" Path="Title" />
                    <Binding Source="{StaticResource motto}" Path="Words" />
                  </MultiBinding>
                </TextBlock.Text>
              </TextBlock>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, Tile.On);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in new[] { Tile.On, Tile.Off })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
            Assert.Contains($">{model.Text}<", expected, StringComparison.Ordinal);
        }

        var body = EngineFixture.Body(planned, Tile.Off);
        Assert.Contains(">Static title<", body, StringComparison.Ordinal);
        Assert.Contains(">Resource words<", body, StringComparison.Ordinal);
        Assert.Contains(">Hello from a phrase<", body, StringComparison.Ordinal);
        Assert.Contains(">Static title / Resource words<", body, StringComparison.Ordinal);
        Assert.Contains(">classed<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">hidden<", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-61")]
    public async Task SourceThatIsADataContextReadsItsScopeAsync()
    {
        const string Xaml = """
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="x:String">
                  <StackPanel>
                    <TextBlock Text="{Binding}" />
                    <TextBlock m:OuterText.Path="Text" />
                  </StackPanel>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """;
        foreach (var (from, to) in new[] { (Tile.On, Tile.Off), (Tile.Off, Tile.On) })
        {
            var planned = await fixture.CompileAsync(Xaml, from);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, to), to);

            Assert.True(planned.Reusable, planned.Failure);
            Assert.Equal(expected, EngineFixture.Body(planned, to));
            Assert.Equal(expected, EngineFixture.Body(saved, to));
            Assert.Contains($">{to.Text}<", expected, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-62")]
    public async Task SourcesThePlanCannotFollowAreReportedAsync()
    {
        foreach (var body in new[]
        {
            """<TextBlock Text="{m:Root DataContext.Text}" />""",
            """
            <TextBlock>
              <TextBlock.Text>
                <MultiBinding StringFormat="{}{0} {1}">
                  <Binding Source="{x:Static m:Labels.Current}" Path="Title" />
                  <Binding Path="Text" />
                </MultiBinding>
              </TextBlock.Text>
            </TextBlock>
            """,
        })
        {
            var page = await fixture.CompileAsync(body, Tile.On);

            Assert.False(page.Reusable, body);
            Assert.Contains("TextBlock.Text", page.Failure, StringComparison.Ordinal);
            Assert.Contains("through Source", page.Failure, StringComparison.Ordinal);
        }
    }
}
