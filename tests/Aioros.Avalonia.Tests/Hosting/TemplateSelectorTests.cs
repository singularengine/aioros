using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class TemplateSelectorTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-131")]
    public async Task SelectorsThatBuildInCodeAreLearnedForEachValueAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Resources><m:DoneMarks x:Key="Marks" /></StackPanel.Resources>
              <StackPanel.DataTemplates>
                <DataTemplate x:DataType="m:Circle"><TextBlock Text="{Binding Radius}" /></DataTemplate>
              </StackPanel.DataTemplates>
              <ContentControl Content="{Binding Mood}" ContentTemplate="{x:Static m:MoodIcons.Instance}" Padding="4" />
              <ContentControl Content="{Binding Done}" ContentTemplate="{StaticResource Marks}" />
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, new Feeling(Mood.Busy, true));
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var feeling in new[] { new Feeling(Mood.Calm, false), new Feeling(Mood.Busy, true), new Feeling(Mood.Away, false) })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, feeling), feeling);
            Assert.Equal(expected, EngineFixture.Body(planned, feeling));
            Assert.Equal(expected, EngineFixture.Body(saved, feeling));
        }

        Assert.Contains(">done<", EngineFixture.Body(saved, new Feeling(Mood.Calm, true)), StringComparison.Ordinal);
        Assert.Contains(">open<", EngineFixture.Body(saved, new Feeling(Mood.Calm, false)), StringComparison.Ordinal);
        Assert.NotEqual(EngineFixture.Body(saved, new Feeling(Mood.Calm, true)), EngineFixture.Body(saved, new Feeling(Mood.Busy, true)));

        var person = await fixture.CompileAsync("""<StackPanel><StackPanel.Resources><m:NameTags x:Key="Tags" /></StackPanel.Resources><ContentControl Content="{Binding Who}" ContentTemplate="{StaticResource Tags}" /></StackPanel>""", new Feeling(Mood.Calm, true, new Person("Ann")));
        Assert.False(person.Reusable);
        Assert.Contains("NameTags", person.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-132")]
    public async Task SelectorsOfKeyedTemplatesPickTheirTemplateOnEachRenderAsync()
    {
        const string Templates = """
            <m:NoticeTemplates x:Key="Picker">
              <m:NoticeTemplates.DefaultTemplate>
                <DataTemplate x:DataType="m:Banner"><TextBlock Text="{Binding Title}" FontWeight="Bold" /></DataTemplate>
              </m:NoticeTemplates.DefaultTemplate>
              <DataTemplate x:Key="info" x:DataType="m:Notice"><TextBlock Text="{Binding Text}" /></DataTemplate>
              <DataTemplate x:Key="warn" x:DataType="m:Notice"><Border Background="Orange" Padding="2"><TextBlock Text="{Binding Text}" Foreground="Red" /></Border></DataTemplate>
            </m:NoticeTemplates>
            """;
        var xaml = $"""
            <StackPanel>
              <StackPanel.Resources>{Templates}</StackPanel.Resources>
              <ItemsControl ItemsSource="{"{Binding Items}"}" ItemTemplate="{"{StaticResource Picker}"}" />
              <ContentControl Content="{"{Binding Lead}"}" ContentTemplate="{"{StaticResource Picker}"}" />
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(xaml, new Notices(new Notice("info", "hello"), new Banner("top")));
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var notices in new[]
        {
            new Notices(new Notice("warn", "careful"), new Notice("info", "fyi"), new Banner("news"), new Notice("warn", "again")),
            new Notices(new Banner("only")),
            new Notices(),
        })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(xaml, notices), notices);
            Assert.Equal(expected, EngineFixture.Body(planned, notices));
            Assert.Equal(expected, EngineFixture.Body(saved, notices));
        }

        Assert.Contains(">careful<", EngineFixture.Body(saved, new Notices(new Notice("warn", "careful"))), StringComparison.Ordinal);

        foreach (var (selector, name) in new[]
        {
            ("""<m:NoticeTemplates x:Key="Picker"><DataTemplate x:Key="info" x:DataType="m:Notice"><TextBlock Text="{Binding Text}" /></DataTemplate><DataTemplate x:Key="odd"><TextBlock Text="odd" /></DataTemplate></m:NoticeTemplates>""", "NoticeTemplates"),
            ("""<m:TunedTemplates x:Key="Picker" Prefix="x"><DataTemplate x:Key="xinfo" x:DataType="m:Notice"><TextBlock Text="{Binding Text}" /></DataTemplate></m:TunedTemplates>""", "TunedTemplates"),
        })
        {
            var page = await fixture.CompileAsync($$"""<StackPanel><StackPanel.Resources>{{selector}}</StackPanel.Resources><ItemsControl ItemsSource="{Binding Items}" ItemTemplate="{StaticResource Picker}" /></StackPanel>""", new Notices(new Notice("info", "hello")));
            Assert.False(page.Reusable, selector);
            Assert.Contains(name, page.Failure, StringComparison.Ordinal);
        }
    }
}
