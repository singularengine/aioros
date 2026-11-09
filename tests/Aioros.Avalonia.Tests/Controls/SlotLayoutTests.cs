using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class SlotLayoutTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "LAYOUT-70")]
    public async Task AnAlignedBoxIsNeverWiderThanItsSlotAsync()
    {
        const string Body = """
            <StackPanel Spacing="16">
              <StackPanel MaxWidth="880" HorizontalAlignment="Left" Spacing="8">
                <TextBlock TextWrapping="Wrap" Text="Left column with a lead long enough to wrap on a phone, as a component page describes itself." />
                <TextBlock Width="560" TextAlignment="Right" Text="Wide" />
              </StackPanel>
              <StackPanel HorizontalAlignment="Center" Margin="12,0" Spacing="8">
                <TextBlock TextWrapping="Wrap" Text="Centered column with a lead long enough to wrap on a phone, inside its margins." />
                <TextBlock Width="560" TextAlignment="Right" Text="Far" />
              </StackPanel>
            </StackPanel>
            """;
        var desktop = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 390, 600))).Texts;
        var web = await BrowserLayout.TextsAsync(await fixture.CompileAsync(Body), 390, 600);

        Assert.Equal(Slots(desktop), Slots(web));
        Assert.Equal((true, true, 475, 475), Slots(web));
    }

    private static (bool Left, bool Centered, int Wide, int Far) Slots(ImmutableArray<LaidOutText> texts)
    {
        var left = texts.Single(text => text.Text.StartsWith("Left", StringComparison.Ordinal));
        var centered = texts.Single(text => text.Text.StartsWith("Centered", StringComparison.Ordinal));
        var wide = texts.Single(text => text.Text == "Wide");
        var far = texts.Single(text => text.Text == "Far");
        return (
            left.X + left.Width <= 390 && left.Height > wide.Height * 1.5,
            centered.X >= 12 && centered.X + centered.Width <= 378 && centered.Height > far.Height * 1.5,
            (int)Math.Round(wide.X + wide.Width),
            (int)Math.Round(far.X + far.Width));
    }
}
