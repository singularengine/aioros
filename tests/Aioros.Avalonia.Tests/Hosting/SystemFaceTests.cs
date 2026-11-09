using Aioros.Avalonia.Tests.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class SystemFaceTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-281")]
    public async Task AWeightAFamilyLacksGetsItsRealNearestFaceWhateverWasAskedFirstAsync()
    {
        const string Family = "Liberation Serif";
        await fixture.CompileAsync($"""<StackPanel TextElement.FontFamily="{Family}"><TextBlock Text="Plain" /></StackPanel>""");

        var found = await fixture.Host.InvokeAsync(() =>
        {
            FontManager.Current.TryGetGlyphTypeface(new Typeface(Family), out _);
            var semibold = new Typeface(Family, FontStyle.Normal, FontWeight.SemiBold);
            FontManager.Current.TryGetGlyphTypeface(semibold, out var face);
            using var semiboldText = new TextLayout("Semi Avalonia", semibold, 16, null);
            using var boldText = new TextLayout("Semi Avalonia", new Typeface(Family, FontStyle.Normal, FontWeight.Bold), 16, null);
            return (face?.FamilyName, face?.Weight, face?.FontSimulations, Width: Math.Round(semiboldText.Width, 2), Bold: Math.Round(boldText.Width, 2));
        }, CancellationToken.None);

        Assert.Equal((Family, (FontWeight?)FontWeight.Bold, (FontSimulations?)FontSimulations.None), (found.FamilyName, found.Weight, found.FontSimulations));
        Assert.Equal(found.Bold, found.Width);
    }
}
