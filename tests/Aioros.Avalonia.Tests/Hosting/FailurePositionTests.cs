using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class FailurePositionTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-306")]
    public async Task APlanFailureNamesItsPlaceInTheXamlAsync()
    {
        var keys = await CompileAsync("keys", """
            <StackPanel>
                <StackPanel.KeyBindings><KeyBinding Gesture="Ctrl+Q" Command="{x:Static m:KeyCommands.Quit}" /></StackPanel.KeyBindings>
                <TextBlock Text="q" />
              </StackPanel>
            """, "<StackPanel>");
        var tag = await CompileAsync("tag", """
            <StackPanel>
                <TextBlock Text="Tagged" />
                <TextBlock Text="x" Tag="{Binding Name}" />
              </StackPanel>
            """, "<TextBlock Text=\"x\"");
        var planned = await CompileAsync("planned", """<TextBlock Text="{Binding Name}" />""", "<TextBlock");

        Assert.Contains("KeyBinding.Command", keys.Page.Failure, StringComparison.Ordinal);
        Assert.Equal(keys.Expected, keys.Page.FailureAt);
        Assert.Contains("TextBlock.Tag", tag.Page.Failure, StringComparison.Ordinal);
        Assert.Equal(tag.Expected, tag.Page.FailureAt);
        Assert.True(planned.Page.Reusable, planned.Page.Failure);
        Assert.Null(planned.Page.FailureAt);
        Assert.Equal("Pages/keys/Page.axaml(3,4)", new SourcePosition("Pages/keys/Page.axaml", 3, 4).ToString());
    }

    private async Task<(CompiledPage Page, SourcePosition Expected)> CompileAsync(string name, string body, string element)
    {
        var source = $"Pages/{name}/Page.axaml";
        var xaml = ControlHarness.Xaml(body, nameof(Greeter));
        fixture.Sources.Override(source, xaml);
        var lines = xaml.Split('\n');
        var line = Array.FindIndex(lines, text => text.Contains(element, StringComparison.Ordinal));
        var page = await fixture.RenderAsync(new PageDefinition($"/{name}", source, _ => null, []), "light", new Greeter());
        return (page, new SourcePosition(Path.Combine(EngineFixture.ProjectDirectory, source), line + 1, lines[line].IndexOf(element, StringComparison.Ordinal) + 2));
    }
}
