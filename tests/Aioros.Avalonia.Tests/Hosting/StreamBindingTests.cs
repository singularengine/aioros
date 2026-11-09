using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class StreamBindingTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-120")]
    public async Task StreamBindingsShowTheLatestValueAsync()
    {
        const string Xaml = """
            <StackPanel>
              <TextBlock Text="{Binding Title^}" IsVisible="{Binding Title^, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />
              <TextBlock Text="{Binding Note^}" />
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, Headline.First);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in new Func<Headline>[] { () => Headline.First, () => Headline.Second, () => Headline.Blank })
        {
            var rendered = model();
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, rendered), rendered);
            var fresh = model();
            Assert.Equal(expected, EngineFixture.Body(planned, fresh));
            Assert.Equal(expected, EngineFixture.Body(saved, fresh));
            Assert.Equal(0, fresh.Title.Open);
            Assert.Contains($">{await fresh.Note}<", expected, StringComparison.Ordinal);
        }

        Assert.Contains(">Hello<", EngineFixture.Body(planned, Headline.First), StringComparison.Ordinal);
        Assert.Contains(">World<", EngineFixture.Body(planned, Headline.Second), StringComparison.Ordinal);

        var quiet = await fixture.CompileAsync(Xaml, Headline.Quiet);
        Assert.False(quiet.Reusable);
        Assert.Contains("'Title^'", quiet.Failure, StringComparison.Ordinal);
    }
}
